namespace PmTracker.Web.Models.ViewModels.Vyjadreni;

/// <summary>
/// Spec 2026-04-29-modal-vyjadreni-dropdown §3 — option v dropdown selectoru
/// per bublinu vyjadreni. IsDisabled + DisabledReason řídí disabled stav v
/// gov-form-select (1:1 / chronologie validation, computed server-side).
/// </summary>
/// <param name="KrokKey">UUID kroku — value v dropdown option (FK na CiselnikHarmonogramTypu.KrokKey).</param>
/// <param name="KrokPoradi">Číslo kroku 1–10 — pro UI label "Krok N: Nazev".</param>
/// <param name="Nazev">Lidsky čitelný název kroku z ciselníku.</param>
/// <param name="IsDisabled">True pokud option nelze vybrat (1:1 broken nebo chronologie broken).</param>
/// <param name="DisabledReason">Tooltip text pro disabled option ("Přiřazen bublině z dd.mm.yyyy" / "Porušila by se chronologie kroku N").</param>
public sealed record KrokOptionViewModel(
    Guid KrokKey,
    int KrokPoradi,
    string Nazev,
    bool IsDisabled,
    string? DisabledReason);
