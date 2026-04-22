namespace PmTracker.Web.Services.Security;

/// <summary>
/// Úroveň permission key. Určuje, zda key potřebuje projektový kontext při kontrole
/// (<see cref="Project"/>), nebo je globální a nepotřebuje <c>projektId</c> (<see cref="Global"/>).
/// </summary>
/// <remarks>
/// Ukládá se v DB jako string přes EF <c>HasConversion</c> s <c>ToUpperInvariant</c>.
///
/// Viz glossary v <see cref="PermissionSeedConfiguration"/> (přidá se v B0-4).
/// </remarks>
public enum PermissionScopeLevel
{
    /// <summary>Key se kontroluje bez projektového kontextu (např. <c>people.manage</c>, <c>settings.view</c>).</summary>
    Global,

    /// <summary>Key potřebuje konkrétní projekt při kontrole (např. <c>projects.edit</c>, <c>records.edit</c>).</summary>
    Project
}
