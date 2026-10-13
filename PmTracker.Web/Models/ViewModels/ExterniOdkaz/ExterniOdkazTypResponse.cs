namespace PmTracker.Web.Models.ViewModels.ExterniOdkaz;

/// <summary>
/// Odpověď GET /ExterniOdkaz/Typ (2026-10-13) — jen metadata tiketu pro otypování externí
/// vazby v editoru návrhu: typ (NES/PMP/PNF, normalizovaný), stručně a popis (PNF jím
/// předvyplní text požadavku). Žádná vyjádření ani vytěžené datumy.
/// </summary>
public sealed record ExterniOdkazTypResponse(bool Nalezeno, string Cislo, string? Typ, string? Strucne, string? Popis);
