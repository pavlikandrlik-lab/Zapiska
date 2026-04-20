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
