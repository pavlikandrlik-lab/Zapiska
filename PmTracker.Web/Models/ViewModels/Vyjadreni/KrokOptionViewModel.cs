namespace PmTracker.Web.Models.ViewModels.Vyjadreni;

/// <summary>
/// Spec 2026-04-29-modal-vyjadreni-dropdown §3 — option v dropdown selectoru
/// per bublinu vyjadreni. IsDisabled + DisabledReason řídí disabled stav v
/// gov-form-select (1:1 / chronologie validation, computed server-side).
/// </summary>
/// <param name="KrokPoradi">Číslo kroku 1–10 — value v dropdown option + UI label "Krok N: Nazev".</param>
/// <param name="Nazev">Lidsky čitelný název kroku z pevné definice kroků.</param>
/// <param name="IsDisabled">True pokud option nelze vybrat (1:1 broken nebo chronologie broken).</param>
/// <param name="DisabledReason">Tooltip text pro disabled option ("Přiřazen bublině z dd.mm.yyyy" / "Porušila by se chronologie kroku N").</param>
public sealed record KrokOptionViewModel(
    int KrokPoradi,
    string Nazev,
    bool IsDisabled,
    string? DisabledReason);
