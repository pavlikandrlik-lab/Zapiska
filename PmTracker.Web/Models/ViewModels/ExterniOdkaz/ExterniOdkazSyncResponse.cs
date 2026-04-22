namespace PmTracker.Web.Models.ViewModels.ExterniOdkaz;

/// <summary>
/// Odpověď endpointu POST /ExterniOdkaz/Sync — výsledek lookupu 6místného čísla
/// v intranetNEW ServiceDesku.
/// </summary>
public sealed record ExterniOdkazSyncResponse(
    bool Nalezeno,
    string Cislo,
    string? Typ,
    string? Strucne);
