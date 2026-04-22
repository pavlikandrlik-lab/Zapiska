namespace PmTracker.Web.Services.Security;

/// <summary>
/// Šířka grantu v jednotlivém role → permission mappingu. Určuje, na které entity se grant
/// rozšíří v rámci <see cref="RoleScope"/> role.
/// </summary>
/// <remarks>
/// Ukládá se do DB jako string přes EF <c>HasConversion</c> s <c>ToUpperInvariant</c>.
///
/// Viz glossary v <see cref="PermissionSeedConfiguration"/> (přidá se v B0-4).
/// </remarks>
public enum ScopeMode
{
    /// <summary>Platí pro všechny entity v rozsahu role (např. ADM_PROJ = všechny záznamy na projektech, kde má roli).</summary>
    All,

    /// <summary>Platí jen pro explicitně vybraná projekty (role_permission_projects). Aktuálně podporováno v DB CHECK constraintu.</summary>
    Include,

    /// <summary>Platí jen pro entity vlastněné osobou (např. komentáře autored vlastní osobou). Přidá se v Phase C.</summary>
    Own,

    /// <summary>Platí jen pro entity v subsystému osoby (např. METODIK smí komentovat jen záznamy svého subsystému). Přidá se v Phase C.</summary>
    Subsystem
}
