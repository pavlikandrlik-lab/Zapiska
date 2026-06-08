namespace PmTracker.Web.Models.ViewModels;

public sealed class AppDateFieldViewModel
{
    public required string Name { get; init; }
    public string IsoValue { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public bool Locked { get; init; }
    public string AriaLabel { get; init; } = "Otevřít kalendář";
    public string? ContainerCssClass { get; init; }

    /// <summary>
    /// FIX 2026-05-05: opt-in clear button (✕) vedle kalendářového trigger. Default false
    /// (zachování existing chování — krok 1 / základní údaje / Jednání nemají clear).
    /// Manuální kroky 2/5/8/9 v harmonogramu nastaví true, aby user mohl vyprázdnit datum
    /// (žádný jiný způsob neexistuje — pm-date-field input je readonly, kalendář datum
    /// jen vybírá, ne vyprazdňuje).
    /// </summary>
    public bool Clearable { get; init; }

    public IReadOnlyDictionary<string, object?> ExtraDataAttributes { get; init; }
        = new Dictionary<string, object?>();
}
