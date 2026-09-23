using System.Text.RegularExpressions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

public interface IExternalLinkProposalTypeResolver
{
    /// <summary>
    /// Autoritativně dohledá Typ (NES/PMP/PNF) každé externí vazby ze ServiceDesku podle čísla
    /// tiketu a zapíše ho do commandu. Vyhodí <see cref="RecordValidationException"/>, pokud typ
    /// nelze určit (ticket nenalezen / SD vypnuté). Návrh tak vždy nese platný Typ, nezávisle na
    /// klientském JS. Kolekce se mutuje in-place.
    /// </summary>
    Task ResolveAndValidateAsync(IReadOnlyList<SaveRecordExterniVazbaCommand> links, CancellationToken ct = default);
}

/// <summary>
/// Typ externí vazby je metadata tiketu (ne „skutečnost"). V editoru návrhu se harvest 4 datumů
/// odkládá až po založení, ale Typ musí být zachycen už při odeslání — jinak schválení (SaveRecord)
/// padne na external_type_required. Tento resolver je serverová autorita nad Typem.
/// </summary>
public sealed class ExternalLinkProposalTypeResolver : IExternalLinkProposalTypeResolver
{
    private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);

    private readonly ITicketingQueryService _ticketing;

    public ExternalLinkProposalTypeResolver(ITicketingQueryService ticketing) => _ticketing = ticketing;

    public async Task ResolveAndValidateAsync(IReadOnlyList<SaveRecordExterniVazbaCommand> links, CancellationToken ct = default)
    {
        if (links.Count == 0)
        {
            return;
        }

        var issues = new List<RecordValidationIssue>();
        for (var index = 0; index < links.Count; index++)
        {
            var link = links[index];
            var cislo = (link.Cislo ?? string.Empty).Trim();
            var typ = (link.Typ ?? string.Empty).Trim();
            var rowPrefix = $"ExterniVazby[{index}]";

            // Prázdný řádek (uživatel přidal vazbu a nic nevyplnil) — přeskoč, jako SaveRecord.
            if (cislo.Length == 0 && typ.Length == 0)
            {
                continue;
            }

            if (!SixDigits.IsMatch(cislo))
            {
                issues.Add(new RecordValidationIssue(
                    $"{rowPrefix}.Cislo",
                    "Vyplňte číslo externí vazby (přesně 6 cifer).",
                    "external",
                    "external_number_required",
                    cislo));
                continue;
            }

            var dto = await _ticketing.GetZaznamAsync(cislo, ct);
            var resolvedType = NormalizeType(dto?.TypZaznamu);
            if (dto is null || resolvedType.Length == 0)
            {
                issues.Add(new RecordValidationIssue(
                    $"{rowPrefix}.Cislo",
                    $"Ticket {cislo} nebyl v ServiceDesku nalezen — typ externí vazby nelze určit.",
                    "external",
                    "external_sd_ticket_not_found",
                    cislo));
                continue;
            }

            // Server je autoritativní zdroj Typu — přepíše hodnotu poslanou klientem.
            link.Typ = resolvedType;
        }

        if (issues.Count > 0)
        {
            throw new RecordValidationException(
                issues[0].Message,
                issues,
                $"ExternalLinkProposalTypeResolver: {issues.Count} chyba/y externích vazeb návrhu.");
        }
    }

    private static string NormalizeType(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToUpperInvariant();
}
