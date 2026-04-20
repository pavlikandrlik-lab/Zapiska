namespace PmTracker.Web.Models.ViewModels;

public sealed class ProjektTymTabViewModel
{
    public int ProjektId { get; init; }
    public IReadOnlyList<ProjectRoleGridRowViewModel> AktivniRole { get; init; } = Array.Empty<ProjectRoleGridRowViewModel>();
    public IReadOnlyList<ProjectRoleHistoryGridRowViewModel> HistorieRoli { get; init; } = Array.Empty<ProjectRoleHistoryGridRowViewModel>();
    public IReadOnlyList<ProjectTeamSubsystemRowViewModel> AktivniSubsystemyProjektu { get; init; } = Array.Empty<ProjectTeamSubsystemRowViewModel>();
    public IReadOnlyList<ProjectMemberCandidateViewModel> DostupneOsobyProRole { get; init; } = Array.Empty<ProjectMemberCandidateViewModel>();
    public IReadOnlyList<ProjectSubsystemOptionViewModel> DostupneProjektoveSubsystemy { get; init; } = Array.Empty<ProjectSubsystemOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleProjektu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> RoleSubsystemu { get; init; } = Array.Empty<LookupOptionViewModel>();
    public IReadOnlyList<LookupOptionViewModel> DostupneSubsystemy { get; init; } = Array.Empty<LookupOptionViewModel>();
    public bool CanManageTeam { get; set; }
}

public sealed class ProjectMemberCandidateViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}

public sealed class ProjectRoleAssignmentViewModel
{
    public int ProjektRoleId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectRoleGridRowViewModel
{
    public int AssignmentId { get; init; }
    public required string AssignmentKind { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public required string RoleTypeLabel { get; init; }
    public string? SubsystemKod { get; init; }
    public string? SubsystemNazev { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectRoleHistoryGridRowViewModel
{
    public int AssignmentId { get; init; }
    public required string AssignmentKind { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public required string RoleTypeLabel { get; init; }
    public string? SubsystemKod { get; init; }
    public string? SubsystemNazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public DateTime? DatumOdebrani { get; init; }
}

public sealed class ProjectRoleHistoryItemViewModel
{
    public int ProjektRoleId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public DateTime? DatumOdebrani { get; init; }
}

public sealed class ProjectSubsystemViewModel
{
    public int ProjektSubsystemId { get; init; }
    public int SubsystemId { get; init; }
    public int Poradi { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectTeamSubsystemRowViewModel
{
    public int ProjektSubsystemId { get; init; }
    public int SubsystemId { get; init; }
    public int Poradi { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public bool CanMoveUp { get; init; }
    public bool CanMoveDown { get; init; }
}

public sealed class ProjectSubsystemOptionViewModel
{
    public int ProjektSubsystemId { get; init; }
    public int SubsystemId { get; init; }
    public required string Label { get; init; }
    public required string Kod { get; init; }
    public required string Nazev { get; init; }
}

public sealed class ProjectSubsystemRoleAssignmentViewModel
{
    public int ProjektSubsystemRoleId { get; init; }
    public int ProjektSubsystemId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string SubsystemKod { get; init; }
    public required string SubsystemNazev { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
    public DateTime DatumPrirazeni { get; init; }
}

public sealed class ProjectSubsystemRoleHistoryItemViewModel
{
    public int ProjektSubsystemRoleId { get; init; }
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public required string SubsystemKod { get; init; }
    public required string SubsystemNazev { get; init; }
    public required string RoleKod { get; init; }
    public required string RoleNazev { get; init; }
    public DateTime DatumPrirazeni { get; init; }
    public DateTime? DatumOdebrani { get; init; }
}

public sealed class TeamMemberViewModel
{
    public int OsobaId { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public required string Role { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}

public sealed class TeamCandidateViewModel
{
    public int Id { get; init; }
    public required string Osoba { get; init; }
    public string? Email { get; init; }
    public string? Organizace { get; init; }
    public string? OrganizacniCelek { get; init; }
}
