using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels;

public sealed class SaveProjectCommand
{
    public int? Id { get; set; }

    [Required]
    [StringLength(255)]
    public string Nazev { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Zkratka { get; set; } = string.Empty;

    [Required]
    public string Stav { get; set; } = string.Empty;

    public bool PouzivatIdentJednani { get; set; }
}

public sealed class SoftDeleteProjectCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "Potvrďte smazání projektu.")]
    public bool PotvrditSmazani { get; set; }
}

public sealed class SaveRecordCommand
{
    public int? Id { get; set; }

    [Required]
    public int ProjektId { get; set; }

    [Required]
    public string Kategorie { get; set; } = string.Empty;

    public string? TypUkolu { get; set; }

    [Required]
    public string Stav { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Nazev { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Cil { get; set; }

    public string? Popis { get; set; }

    [Required(ErrorMessage = "Vyberte vlastníka z nabídky.")]
    public int? VlastnikId { get; set; }

    [Required]
    public DateTime DatumZalozeni { get; set; }

    [Required]
    public DateTime TerminUkonceni { get; set; }

    [Required]
    public string Subsystem { get; set; } = string.Empty;

    public int CisloZaznamu { get; set; }

    public List<int> VybraniSpolupracovniciIds { get; set; } = new();

    public List<SaveRecordExterniVazbaCommand> ExterniVazby { get; set; } = new();

    public List<SaveRecordHarmonogramValueCommand> HarmonogramHodnoty { get; set; } = new();

    public string? EditorTab { get; set; }

    public string? Presentation { get; set; }

    public string? ReturnUrl { get; set; }

    public int? JednaniIdProCislo { get; set; }

    public string? UiContext { get; set; }

    public int? MeetingId { get; set; }
}

public sealed class DeleteRecordCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ZaznamId { get; set; }

    public bool PotvrditSmazani { get; set; }
}

public sealed class AssignMeetingIdentifierCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ZaznamId { get; set; }

    [Required]
    public int JednaniId { get; set; }
}

public sealed class SaveRecordExterniVazbaCommand
{
    public int Id { get; set; }
    public string? Typ { get; set; }
    public string? Cislo { get; set; }
    public string? PredpokladanaCena { get; set; }
    public string? Vyzva { get; set; }
    public DateTime? DatumObjednani { get; set; }
    public DateTime? PlanDodani { get; set; }
    public DateTime? DatumDodani { get; set; }
    public DateTime? DatumPrevzeti { get; set; }
}

public sealed class SaveRecordHarmonogramValueCommand
{
    public int TypId { get; set; }
    public int Hodnota { get; set; }
}

public sealed class SaveMeetingCommand
{
    public int? Id { get; set; }

    [Required]
    public int ProjektId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Číslo jednání musí být větší než 0.")]
    public int CisloJednani { get; set; }

    [Required]
    public DateTime DatumPlanovane { get; set; }

    [Required]
    public TimeOnly CasZacatek { get; set; }

    public string? Misto { get; set; }

    [Required]
    public string StavJednani { get; set; } = string.Empty;
}

public sealed class DeleteMeetingCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public int ProjektId { get; set; }
}

public sealed class SaveMeetingStatusCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public string Stav { get; set; } = string.Empty;

    public bool UzavritJednani { get; set; }

    public bool OtevritJednani { get; set; }
}

public sealed class SaveMeetingNoteCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public int ZaznamId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;
}

public sealed class SaveAttendanceCommand
{
    [Required]
    public int JednaniId { get; set; }

    [Required]
    public int OsobaId { get; set; }

    [Required]
    public string StavUcasti { get; set; } = string.Empty;
}

public sealed class AddMeetingParticipantCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int JednaniId { get; set; }

    [Required(ErrorMessage = "Vyberte osobu z nabídky.")]
    public int? OsobaId { get; set; }
}

public sealed class AssignProjectRoleCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required(ErrorMessage = "Vyberte osobu z nabídky.")]
    public int? OsobaId { get; set; }

    [Required]
    public string RoleKod { get; set; } = string.Empty;
}

public sealed class DeactivateProjectRoleCommand
{
    [Required]
    public int ProjektRoleId { get; set; }
}

public sealed class AssignProjectSubsystemCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public string SubsystemKod { get; set; } = string.Empty;
}

public sealed class DeactivateProjectSubsystemCommand
{
    [Required]
    public int ProjektSubsystemId { get; set; }
}

public static class ProjectSubsystemReorderDirections
{
    public const string Up = "up";
    public const string Down = "down";
}

public sealed class ReorderProjectSubsystemCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ProjektSubsystemId { get; set; }

    [Required]
    [RegularExpression("^(up|down)$", ErrorMessage = "Neplatný směr přesunu subsystému.")]
    public string Direction { get; set; } = ProjectSubsystemReorderDirections.Up;
}

public sealed class AssignProjectSubsystemRoleCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ProjektSubsystemId { get; set; }

    [Required(ErrorMessage = "Vyberte osobu z nabídky.")]
    public int? OsobaId { get; set; }

    [Required]
    public string RoleKod { get; set; } = string.Empty;
}

public sealed class DeactivateProjectSubsystemRoleCommand
{
    [Required]
    public int ProjektSubsystemRoleId { get; set; }
}

public sealed class SaveTeamMemberCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required(ErrorMessage = "Vyberte osobu z nabídky.")]
    public int? OsobaId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;
}

public sealed class RemoveTeamMemberCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int OsobaId { get; set; }
}

public sealed class SaveManualPersonCommand
{
    public int? Id { get; set; }

    [Required]
    public string Jmeno { get; set; } = string.Empty;

    [Required]
    public string Prijmeni { get; set; } = string.Empty;

    public string? Titul { get; set; }

    public string? Email { get; set; }

    public string? Organizace { get; set; }

    public string? OrganizacniCelek { get; set; }

    public bool LocationLocked { get; set; }
}

public sealed class SaveAdPersonCommand
{
    [Required]
    public string VyhledavaciRetezec { get; set; } = string.Empty;

    [Required]
    public string Jmeno { get; set; } = string.Empty;

    [Required]
    public string Prijmeni { get; set; } = string.Empty;

    public string? Titul { get; set; }

    [Required(ErrorMessage = "Nejprve vyberte osobu z AD výsledků.")]
    public Guid? GuidAd { get; set; }

    public string? AdLogin { get; set; }
    public string? AdCompany { get; set; }
    public string? AdDepartment { get; set; }
    public string? Organizace { get; set; }
    public string? OrganizacniCelek { get; set; }
    public bool LocationLocked { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed class AddCommentCommand
{
    [Required]
    public int ZaznamId { get; set; }

    [Required]
    public int JednaniId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;
}

public sealed class UpdateCommentCommand
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;
}

public sealed class DeleteCommentCommand
{
    [Required]
    public int Id { get; set; }
}

public sealed class DeletePersonCommand
{
    [Required]
    public int Id { get; set; }
}

public sealed class SaveCiselnikRowCommand
{
    [Required]
    public string Key { get; set; } = string.Empty;

    public int? Id { get; set; }

    [Required]
    public string Kod { get; set; } = string.Empty;

    [Required]
    public string Nazev { get; set; } = string.Empty;

    public bool IsLocked { get; set; }

    public List<string> HodnotyNavic { get; set; } = new();
}

public sealed class DeleteCiselnikRowCommand
{
    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public int Id { get; set; }
}

public sealed class SaveUserRoleAssignmentCommand
{
    [Required]
    public int OsobaId { get; set; }

    [Required]
    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SaveUserRolesForUserCommand
{
    [Required]
    public int OsobaId { get; set; }

    public List<int> RoleIds { get; set; } = new();
}

public sealed class SaveAuthzRoleCommand
{
    public int? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Nazev { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Popis { get; set; }
}

public sealed class ToggleAuthzRoleCommand
{
    [Required]
    public int Id { get; set; }

    public bool IsActive { get; set; }
}

public sealed class SaveAuthzPermissionCommand
{
    public int? Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Klic { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string Nazev { get; set; } = string.Empty;

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [RegularExpression("^(GLOBAL|PROJECT)$", ErrorMessage = "Neplatný rozsah oprávnění.")]
    public string ScopeLevel { get; set; } = "PROJECT";
}

public sealed class ToggleAuthzPermissionCommand
{
    [Required]
    public int Id { get; set; }

    public bool IsActive { get; set; }
}

public sealed class SaveRolePermissionCommand
{
    public int? Id { get; set; }

    [Required]
    public int RoleId { get; set; }

    [Required]
    public int PermissionId { get; set; }

    [Required]
    public string ScopeMode { get; set; } = "ALL";

    public bool IsAllowed { get; set; } = true;

    public List<int> ProjektIds { get; set; } = new();
}

public sealed class DeleteRolePermissionCommand
{
    [Range(1, int.MaxValue, ErrorMessage = "Mapování role/akce nebylo vybráno.")]
    public int Id { get; set; }
}

public sealed class ProposalDecisionCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ProposalId { get; set; }
}

public sealed class PrefillCreateProposalCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ProposalId { get; set; }

    public string? Presentation { get; set; }

    public string? ReturnUrl { get; set; }
}
