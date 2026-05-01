namespace PmTracker.Web.Models.ViewModels;

/// <summary>
/// Sdílený VM pro <c>_ProjectFilterShell.cshtml</c> partial — drží 10 lookup options
/// + identifikátory potřebné pro JS filter init. Mountován v Records i Schedule
/// tabech, scope drží JS pro DOM disambiguation (apply na visible tab).
/// Spec: docs/superpowers/specs/2026-04-30-project-filter-unification-design.md
/// </summary>
public sealed class ProjectFilterShellViewModel
{
    public int ProjektId { get; init; }

    /// <summary>
    /// Doplňuje controller layer (PrepareProjectRecordsTabPresentation /
    /// PrepareProjectScheduleTabPresentation) — service vyrobí shell s 0,
    /// controller settne aktuálního usera. Spec 2026-04-30.
    /// </summary>
    public int CurrentUserOsobaId { get; set; }

    /// <summary>"records" nebo "schedule" — používá se jako data-project-filter-scope.</summary>
    public string Scope { get; init; } = string.Empty;

    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> KategorieMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> StavyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> TypyUkoluMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> VlastniciMoznosti { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> StavyJednaniVyjadreni { get; init; } = Array.Empty<LookupOptionViewModel>();
}
