using PmTracker.Web.TagHelpers;

namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjectModalViewModel
{
    public required string Title { get; init; }
    public required SaveProjectCommand Command { get; init; }
    public IReadOnlyList<LookupOptionViewModel> StavyProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();

    /// <summary>
    /// Katalog Informačních systémů pro dropdown (Plán 5 Sprint B Task 3).
    /// Prázdný = feature fallback (Ticketing:Enabled=false) — dropdown nebude renderovaný.
    /// </summary>
    public IReadOnlyList<PmTracker.Web.Services.ServiceDesk.SdInfoSystem> InfoSystemy { get; init; }
        = Array.Empty<PmTracker.Web.Services.ServiceDesk.SdInfoSystem>();

    public bool IsEdit => Command.Id.HasValue;
}

public sealed class MeetingModalViewModel
{
    public required string Title { get; init; }
    public required SaveMeetingCommand Command { get; init; }
    public IReadOnlyList<LookupOptionViewModel> StavyJednani { get; init; } = Array.Empty<LookupOptionViewModel>();
    public required string ExistingMeetingNumbersCsv { get; init; }
}

public sealed class TeamMemberModalViewModel
{
    public required string Title { get; init; }
    public required SaveTeamMemberCommand Command { get; init; }
    public string? SearchUrl { get; init; }
    public IReadOnlyList<TeamCandidateViewModel> DostupniClenoveTymu { get; init; } = Array.Empty<TeamCandidateViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class AssignProjectRoleModalViewModel
{
    public required string Title { get; init; }
    public required AssignProjectRoleCommand Command { get; init; }
    public string? SearchUrl { get; init; }
    public IReadOnlyList<ProjectMemberCandidateViewModel> DostupneOsoby { get; init; } = Array.Empty<ProjectMemberCandidateViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class AssignProjectSubsystemModalViewModel
{
    public required string Title { get; init; }
    public required AssignProjectSubsystemCommand Command { get; init; }
    public IReadOnlyList<LookupOptionViewModel> Subsystemy { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class AssignProjectSubsystemRoleModalViewModel
{
    public required string Title { get; init; }
    public required AssignProjectSubsystemRoleCommand Command { get; init; }
    public string? SearchUrl { get; init; }
    public IReadOnlyList<ProjectSubsystemOptionViewModel> ProjektSubsystemy { get; init; } = Array.Empty<ProjectSubsystemOptionViewModel>();
    public IReadOnlyList<ProjectMemberCandidateViewModel> DostupneOsoby { get; init; } = Array.Empty<ProjectMemberCandidateViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleSubsystemu { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class ProjectTeamModalOptionsViewModel
{
    public IReadOnlyList<LookupOptionViewModel> RoleProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleSubsystemu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<ProjectSubsystemOptionViewModel> DostupneProjektoveSubsystemy { get; init; } = Array.Empty<ProjectSubsystemOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> DostupneSubsystemy { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class PersonPickerEntryViewModel
{
    public int Id { get; init; }
    public required string Label { get; init; }
    public string? Email { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}

public sealed class PersonPickerSearchResponseViewModel
{
    public IReadOnlyList<PersonPickerEntryViewModel> Results { get; init; } = Array.Empty<PersonPickerEntryViewModel>();
}

public sealed class AddMeetingParticipantModalViewModel
{
    public required string Title { get; init; }
    public required AddMeetingParticipantCommand Command { get; init; }
    public IReadOnlyList<MeetingParticipantCandidateViewModel> DostupneOsoby { get; init; } = Array.Empty<MeetingParticipantCandidateViewModel>();
}

public sealed class DeleteProjectModalViewModel
{
    public required string Title { get; init; }
    public required SoftDeleteProjectCommand Command { get; init; }
    public required string ProjektNazev { get; init; }
    public required string ProjektZkratka { get; init; }
    public required string ProjektStav { get; init; }
}

public sealed class DeleteRecordModalViewModel
{
    public required string Title { get; init; }
    public required DeleteRecordCommand Command { get; init; }
    public required string CisloViditelne { get; init; }
    public required string Nazev { get; init; }
    public required string Kategorie { get; init; }
    public required string Stav { get; init; }
    public int VyjadreniCount { get; init; }
    public int ExterniVazbyCount { get; init; }
    public int SpolupraceCount { get; init; }
    public int HarmonogramCount { get; init; }
}

public sealed class AssignMeetingIdentifierModalViewModel
{
    public required string Title { get; init; }
    public required AssignMeetingIdentifierCommand Command { get; init; }
    public IReadOnlyList<JednaniOptionViewModel> JednaniOptions { get; init; } = Array.Empty<JednaniOptionViewModel>();
    public required string CisloViditelne { get; init; }
}

public sealed class ManualPersonModalViewModel
{
    public required string Title { get; init; }
    public required SaveManualPersonCommand Command { get; init; }
    public IReadOnlyList<LookupOptionViewModel> Organizace { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> OrganizacniCelky { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool IsAdAccount { get; init; }
    public bool IsEdit => Command.Id.HasValue;
}

public sealed class AdPersonModalViewModel
{
    public required string Title { get; init; }
    public required string SearchUrl { get; init; }
    public IReadOnlyList<LookupOptionViewModel> Organizace { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> OrganizacniCelky { get; init; } = Array.Empty<LookupOptionViewModel>();
}

public sealed class ModalFormActionsViewModel
{
    public string SubmitLabel { get; init; } = "Uložit";
    public string CancelLabel { get; init; } = "Zrušit";
    // Fáze 2E: PmButtonVariant nahrazuje volný CSS string.
    // Legacy string property odstraněna (původně "btn primary" / "btn ghost danger").
    public PmButtonVariant SubmitVariant { get; init; }
        = PmButtonVariant.Primary;
    public bool DisableSubmit { get; init; }
}

public sealed class ContextHiddenFieldsViewModel
{
    public int? UserId { get; init; }
    public int? ProjektId { get; init; }
    public string? ReturnUrl { get; init; }
}
