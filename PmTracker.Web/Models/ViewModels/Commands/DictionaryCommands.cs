using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels;

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
