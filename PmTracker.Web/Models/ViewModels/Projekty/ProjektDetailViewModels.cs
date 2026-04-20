namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektDetailViewModel : BaseViewModel
{
    public string PageTitle { get; set; } = string.Empty;
    public string? BackUrl { get; set; }
    public string? BackLabel { get; set; }
    public string ActiveTab { get; set; } = "zaznamy";
    public required ProjektHeaderViewModel Projekt { get; init; }
    public ProjektZaznamyTabViewModel ZaznamyTab { get; init; } = new();
    public ProjektLazyTabShellViewModel HarmonogramTab { get; init; } = new() { TabKey = "harmonogram", LoadingText = "Načítání harmonogramu..." };
    public ProjektLazyTabShellViewModel JednaniTab { get; init; } = new() { TabKey = "jednani", LoadingText = "Načítání jednání..." };
    public ProjektLazyTabShellViewModel TymTab { get; init; } = new() { TabKey = "tym", LoadingText = "Načítání týmu..." };
    public ProjektLazyTabShellViewModel NavrhyTab { get; init; } = new() { TabKey = "navrhy", LoadingText = "Načítání návrhů..." };
    public ProjektHarmonogramTabViewModel? LoadedHarmonogramTab { get; set; }
    public ProjektJednaniTabViewModel? LoadedJednaniTab { get; set; }
    public ProjektTymTabViewModel? LoadedTymTab { get; set; }
    public ProjektNavrhyTabViewModel? LoadedNavrhyTab { get; set; }
    public bool CanCreateMeetings { get; set; }
    public bool CanEditMeetings { get; set; }
    public bool CanManageTeam { get; set; }
    public bool CanManageRecords { get; set; }
    public bool CanManageSchedules { get; set; }
    public bool CanViewProposals { get; set; }
    public bool CanViewDashboard { get; set; }
    public int CurrentUserOsobaId { get; set; }
    public int? TargetRecordId { get; set; }
    public bool TargetRecordOpenComments { get; set; }
    public string? CreateRecordEditorUrl { get; set; }
    public string? ReturnToProjectUrl { get; set; }
    public string? ProjectPrintUrl { get; set; }
    public string? ProjectWordUrl { get; set; }
}

public sealed class ProjektHeaderViewModel
{
    public int Id { get; init; }
    public required string Nazev { get; init; }
    public required string Zkratka { get; init; }
    public required string Stav { get; init; }
    public bool PouzivatIdentJednani { get; init; }
}

public sealed class ProjektLazyTabShellViewModel
{
    public required string TabKey { get; init; }
    public string? LoadUrl { get; set; }
    public string LoadingText { get; set; } = "Načítání...";
}
