using System.Text.RegularExpressions;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// 4 datumy ve metadata kartě externí vazby — výsledek vytěžení z hotline tiketu.
/// Sloupce odpovídají <c>zaznam_externi_odkazy.datum_objednani / plan_dodani / datum_dodani / datum_prevzeti</c>.
/// Každé pole může být <c>null</c> nezávisle (žádná posloupnost není během harvestu vynucována).
/// </summary>
public sealed record PerTicketMetadata(
    DateTime? DatumObjednani,
    DateTime? PlanDodani,
    DateTime? DatumDodani,
    DateTime? DatumPrevzeti);

/// <summary>
/// Pure-logic extractor 4 datumů per externí vazba (per typ tiketu).
/// Spec: <c>2026-04-28-nes-vyjadreni-a-4-datumy-design.md</c> §4.
///
/// Pravidla per typ:
/// <list type="bullet">
///   <item><b>NES</b> — DatumObjednani z fráze „Záznam byl předán dodavateli k řešení.",
///         PlanDodani z <c>HOT_ZAZNAMY.sla_deadline</c> (DB sloupec, ne text), DatumDodani z fráze
///         „Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo" (poslední DESC), DatumPrevzeti z K10.</item>
///   <item><b>PMP/PNF</b> — DatumObjednani z K6 (první výskyt), PlanDodani z PlanDodani fráze
///         + dvojfrázová validace přes K6 v n+1 nebo n+2 (regex DD.MM.YYYY z konce textu),
///         DatumDodani z K4/K7 (poslední DESC), DatumPrevzeti z K10.</item>
///   <item><b>Ostatní typy</b> — všechny 4 datumy <c>null</c>.</item>
/// </list>
/// </summary>
public static class PerTicketMetadataExtractor
{
    private static readonly Regex DatumRegex = new(
        @"\b(\d{1,2})\.(\d{1,2})\.(\d{4})\b",
        RegexOptions.Compiled);

    /// <summary>
    /// Vytěží 4 datumy ze seznamu vyjádření tiketu.
    /// </summary>
    /// <param name="typZaznamu">NES / PMP / PNF (case-insensitive).</param>
    /// <param name="slaDeadline">HOT_ZAZNAMY.sla_deadline — používá se jen pro NES (PlanDodani).</param>
    /// <param name="chronologicalVyjadreni">Vyjádření tiketu seřazená vzestupně podle data (ASC).</param>
    public static PerTicketMetadata Extract(
        string? typZaznamu,
        DateTime? slaDeadline,
        IReadOnlyList<HotVyjadreniDto> chronologicalVyjadreni)
    {
        var typ = (typZaznamu ?? string.Empty).Trim().ToUpperInvariant();
        return typ switch
        {
            "NES" => ExtractNes(slaDeadline, chronologicalVyjadreni),
            "PMP" or "PNF" => ExtractPmpPnf(chronologicalVyjadreni),
            _ => new PerTicketMetadata(null, null, null, null),
        };
    }

    private static PerTicketMetadata ExtractNes(
        DateTime? slaDeadline,
        IReadOnlyList<HotVyjadreniDto> all)
    {
        DateTime? datumObjednani = null;
        DateTime? datumDodani = null;
        DateTime? datumPrevzeti = null;

        foreach (var v in all)
        {
            var kind = HarvestPredicates.ClassifyPopisForNes(v.Popis);
            switch (kind)
            {
                case HarvestPredicateKind.NES_DatumObjednani:
                    datumObjednani ??= v.Datum;            // první výskyt (ASC) vyhrává
                    break;
                case HarvestPredicateKind.NES_DatumDodani:
                    datumDodani = v.Datum;                 // poslední výskyt (DESC) přepíše předchozí
                    break;
                case HarvestPredicateKind.K10_NasazeniArchivace:
                    datumPrevzeti = v.Datum;               // jediný výskyt v praxi
                    break;
            }
        }

        // PlanDodani pro NES — z DB sloupce, ne z textu vyjádření.
        var planDodani = slaDeadline?.Date;
        return new PerTicketMetadata(datumObjednani, planDodani, datumDodani, datumPrevzeti);
    }

    private static PerTicketMetadata ExtractPmpPnf(IReadOnlyList<HotVyjadreniDto> all)
    {
        DateTime? datumObjednani = null;
        DateTime? datumDodani = null;
        DateTime? datumPrevzeti = null;

        foreach (var v in all)
        {
            var kind = HarvestPredicates.ClassifyPopis(v.Popis);
            switch (kind)
            {
                case HarvestPredicateKind.K6_OdeslaniPozadavku:
                    datumObjednani ??= v.Datum;            // první výskyt vyhrává
                    break;
                case HarvestPredicateKind.K4_K7_DodaniReseni:
                    datumDodani = v.Datum;                 // poslední přepíše
                    break;
                case HarvestPredicateKind.K10_NasazeniArchivace:
                    datumPrevzeti = v.Datum;
                    break;
            }
        }

        // PlanDodani — dvojfrázová validace + regex DD.MM.YYYY na konci textu.
        var validated = PlanDodaniDualPhraseValidator.FilterValidated(all);
        DateTime? planDodani = null;
        foreach (var v in validated)
        {
            var datum = TryParseLastDateInText(v.Popis);
            if (datum.HasValue)
            {
                planDodani = datum.Value;                 // poslední validovaný v ASC pořadí vyhrává
            }
        }

        return new PerTicketMetadata(datumObjednani, planDodani, datumDodani, datumPrevzeti);
    }

    private static DateTime? TryParseLastDateInText(string? popis)
    {
        if (string.IsNullOrWhiteSpace(popis)) return null;

        var matches = DatumRegex.Matches(popis);
        if (matches.Count == 0) return null;

        // Poslední match v textu (typicky datum termínu na konci fráze).
        var match = matches[matches.Count - 1];
        if (!int.TryParse(match.Groups[1].Value, out var d)) return null;
        if (!int.TryParse(match.Groups[2].Value, out var m)) return null;
        if (!int.TryParse(match.Groups[3].Value, out var y)) return null;

        try
        {
            return new DateTime(y, m, d);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Neplatné datum (např. 32.13.2026)
            return null;
        }
    }
}
