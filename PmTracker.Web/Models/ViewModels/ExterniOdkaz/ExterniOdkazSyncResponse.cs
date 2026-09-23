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
    IReadOnlyList<ExterniOdkazVyjadreniPreviewDto> Vyjadreni,
    // Popis tiketu z HOT_ZAZNAMY — klient jím předvyplní text požadavku u nové PNF vazby
    // (spec 2026-09-08 §5.4). Poziční parametr až na konci s výchozí hodnotou, aby
    // nerozbil existující volání (stejný postup jako Pid v HotZaznamDto).
    string? Popis = null);

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
