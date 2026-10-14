using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Spec 2026-10-14 §3 — jediné místo s pravidly zápisu SKUTEČNOSTI harmonogramu. Volá ho
/// uložení editoru (RecordService.PersistScheduleKrokyAsync, včetně uložení jen záložky
/// Harmonogram) i schválení návrhu změny harmonogramu (DecisionCommands). Dřív měla každá
/// cesta vlastní pravidla a rozcházela se (vady D2–D4).
///
/// Pravidla:
///  - Kroky 2/5/8/9 jsou vždy ruční (R3): změna se zapíše vždy, přepínač je nemění.
///  - Kroky 1/3/4/6/7/10: změna se zapíše jen v ručním režimu (cílovém, jinak stávajícím);
///    v automatickém je řídí automat.
///  - Přepnutí na automatiku (R2) smaže jejich data, která nedodal automat (ruční i historická)
///    — uživatel 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“.
///  - Krok bez změny se nedotkne — zůstane hodnota i zdroj (oprava D3).
/// </summary>
public static class ScheduleActualApplier
{
    /// <summary>Explicitní změna skutečnosti jednoho kroku. <c>Datum == null</c> = uživatel datum smazal.</summary>
    public sealed record SkutecnostZmena(int Poradi, DateTime? Datum);

    public sealed record Vysledek(bool Zmeneno, bool PrepnutoNaAuto);

    private static readonly int[] AutoKroky = [1, 3, 4, 6, 7, 10];

    /// <summary>Režim, se kterým se zakládá chybějící řádek kroku.</summary>
    public static byte VychoziRezim(int poradi)
        => HarmonogramManualSteps.IsManual(poradi)
            ? (byte)SkutecnostRezimEnum.Manual
            : (byte)SkutecnostRezimEnum.Auto;

    /// <summary>Změna = jiný den (čas se ignoruje); null proti hodnotě je změna (smazání).</summary>
    public static bool JeZmena(DateTime? hodnota, DateTime? puvodni)
        => hodnota?.Date != puvodni?.Date;

    /// <param name="existujici">Řádky kroků záznamu v okamžiku volání (snímek).</param>
    /// <param name="getOrCreate">Vrátí řádek kroku; chybějící založí s <see cref="VychoziRezim"/>.</param>
    /// <param name="zmeny">Jen kroky, které uživatel opravdu změnil.</param>
    /// <param name="cilovyRezim"><c>Auto</c> / <c>Manual</c>, nebo null = režim se nemění.</param>
    public static Vysledek Apply(
        IReadOnlyCollection<ZaznamHarmonogramKrokEntity> existujici,
        Func<int, ZaznamHarmonogramKrokEntity> getOrCreate,
        IReadOnlyList<SkutecnostZmena> zmeny,
        string? cilovyRezim,
        DateTime nowUtc)
    {
        var zmeneno = false;
        var prepnutoNaAuto = false;
        var rezim = HarmonogramAutoFillRule.Normalize(cilovyRezim);
        var bylAuto = HarmonogramAutoFillRule.IsAutoFill(existujici.Select(k => ((int)k.Poradi, k.SkutecnostRezim)));
        var rucniRezim = rezim is not null
            ? rezim == HarmonogramAutoFillRule.Manual
            : !bylAuto;

        if (rezim == HarmonogramAutoFillRule.Manual)
        {
            foreach (var poradi in AutoKroky)
            {
                var row = getOrCreate(poradi);
                if (row.SkutecnostRezim == (byte)SkutecnostRezimEnum.Manual) continue;
                row.SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual;
                row.UpdatedAt = nowUtc;
                zmeneno = true;
            }
        }
        else if (rezim == HarmonogramAutoFillRule.Auto && !bylAuto)
        {
            // R2 (uživatel 2026-10-14: „buď to dělám ručně, nebo to dělá automat, nic mezi“):
            // po zapnutí automatiky smí kroky 1/3/4/6/7/10 nést jen data automatu. Ruční
            // i historická data se mažou — i u kroku, který už Auto byl (smíšený stav ze starých
            // návrhů). Automat je hned doplní ze SD (volající plánuje stažení podle PrepnutoNaAuto).
            foreach (var row in existujici.Where(k => !HarmonogramManualSteps.IsManual(k.Poradi)))
            {
                var mazat = row.SkutecnostDatum is not null
                    && row.SkutecnostZdroj != (byte)SkutecnostZdrojEnum.Automat;
                if (row.SkutecnostRezim == (byte)SkutecnostRezimEnum.Auto && !mazat) continue;
                row.SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto;
                if (mazat)
                {
                    row.SkutecnostDatum = null;
                    row.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo;
                }
                row.UpdatedAt = nowUtc;
            }
            zmeneno = true;
            prepnutoNaAuto = true;
        }

        foreach (var zmena in zmeny.Where(z => z.Poradi is >= 1 and <= 10).GroupBy(z => z.Poradi).Select(g => g.Last()))
        {
            if (!HarmonogramManualSteps.IsManual(zmena.Poradi) && !rucniRezim)
            {
                continue;
            }
            var row = getOrCreate(zmena.Poradi);
            row.SkutecnostDatum = zmena.Datum?.Date;
            row.SkutecnostRezim = (byte)SkutecnostRezimEnum.Manual;
            row.SkutecnostZdroj = zmena.Datum.HasValue
                ? (byte)SkutecnostZdrojEnum.Manual
                : (byte)SkutecnostZdrojEnum.Neznamo;
            row.UpdatedAt = nowUtc;
            zmeneno = true;
        }

        return new Vysledek(zmeneno, prepnutoNaAuto);
    }
}
