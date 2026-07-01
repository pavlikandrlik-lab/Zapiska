namespace PmTracker.Web.Models.ViewModels.Projekty;

public sealed class ProposalFilterShellViewModel
{
    public int ProjektId { get; init; }
    public int CurrentUserOsobaId { get; set; }
    public IReadOnlyList<LookupOptionViewModel> StavyNavrhuMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> TypyNavrhuMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> SubsystemyMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> AutoriMoznosti { get; init; } = [];
    public IReadOnlyList<LookupOptionViewModel> RozhodliMoznosti { get; init; } = [];
}
