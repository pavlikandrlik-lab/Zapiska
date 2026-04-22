namespace PmTracker.Web.Services.Security;

/// <summary>
/// Úroveň role — určuje, jakým způsobem se role přiřazuje k osobě.
/// </summary>
/// <remarks>
/// Hodnoty se v DB uloží jako string přes EF <c>HasConversion&lt;string&gt;()</c>,
/// aby zůstala čitelnost + `CHECK` constraint (viz <c>db_upgrade_1_2_0_authz_role_scope.sql</c>).
///
/// Tři pojmy v autorizačním modelu, nezaměňovat:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="RoleScope"/> (tento typ) — úroveň role. Určuje, JAK se role přiřazuje.
/// Příklad: <c>VLASTNIK_PROJEKTU</c> má <see cref="RoleScope.Project"/> → přiřazuje se přes
/// <c>ObsazeniProjektu</c> na konkrétní projekt.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="PermissionScopeLevel"/> — úroveň permission key. Určuje, zda key potřebuje
/// projektový kontext. Přidá se v B0-3.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ScopeMode"/> — šířka grantu v konkrétním role→permission mappingu. Přidá se v B0-2.
/// </description>
/// </item>
/// </list>
/// </remarks>
public enum RoleScope
{
    /// <summary>Globální role — přiřazená osobě přímo v <c>authz.user_roles</c>. Platí všude.</summary>
    Global,

    /// <summary>Projektová role — přiřazená přes <c>ObsazeniProjektu</c>. Platí na jednom projektu.</summary>
    Project,

    /// <summary>Subsystémová role — přiřazená přes <c>ObsazeniSubsystemuProjektu</c>. Platí na jednom subsystému.</summary>
    Subsystem
}
