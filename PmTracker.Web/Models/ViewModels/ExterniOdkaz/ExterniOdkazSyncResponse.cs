namespace PmTracker.Web.Models.ViewModels.ExterniOdkaz;

/// <summary>
/// Odpověď endpointu POST /ExterniOdkaz/Sync — výsledek lookupu 6místného čísla
/// v intranetNEW ServiceDesku + auto-harvest 4 datumů + vyjádření preview.
///
/// FIX 2026-05-02: rozšířeno o auto-harvest payload pro pre-Save buffer flow.
/// Klient ukládá kompletní response do localStorage pod klíčem
/// <c>pm.externiOdkaz.buffer.{projektId}.{cislo}</c>. Buffer existuje dokud
/// uživatel záznam neuloží — po Save se localStorage vyčistí (DB je pravda).
/// </summary>
public sealed record ExterniOdkazSyncResponse(
    bool Nalezeno,
    string Cislo,
    string? Typ,
    string? Strucne,
    DateTime? DatumObjednani,
    DateTime? PlanDodani,
    DateTime? DatumDodani,
    DateTime? DatumPrevzeti,
    IReadOnlyList<ExterniOdkazVyjadreniPreviewDto> Vyjadreni);

/// <summary>
/// Lehký DTO bublina vyjádření pro pre-Save chat preview (před uložením
/// externí vazby do DB neexistuje žádný HOT_VYJADRENI_VAZBA — bublina je read-only).
/// </summary>
public sealed record ExterniOdkazVyjadreniPreviewDto(
    long Id,
    DateTime Datum,
    string? Typ,
    string? Zpracoval,
    string? Popis);
