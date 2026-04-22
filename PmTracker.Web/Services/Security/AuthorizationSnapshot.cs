namespace PmTracker.Web.Services.Security;

/// <summary>
/// In-memory per-request projekce autorizačních práv jedné osoby.
/// Staví se jednou za HTTP request (viz <c>IAuthorizationService</c>) a odpovídá
/// na volání <see cref="HasPermission(string, int?, int?)"/> bez dalších DB dotazů.
/// </summary>
/// <remarks>
/// NENÍ uložen v DB — zdrojem pravdy je <c>authz.role_permissions</c> + seed
/// (<c>PermissionSeedConfiguration</c>). Invalidace: žádná potřeba, protože je per-request.
///
/// Tato třída tvoří základ kanonické autorizační fasády (Fáze D plánu
/// <c>2026-04-22-authz-phases-c-to-f.md</c>). Nahradí roztříštěný pattern
/// <c>CurrentUserContext.IsSuperAdmin || PermissionGrants.Any(...)</c> v controllerech
/// a service vrstvě jediným volání <c>HasPermission(key, projektId?, subsystemId?)</c>.
///
/// Sémantika <c>PerProjectPermissions</c>: obsahuje oprávnění z OBOU zdrojů —
/// přímých projektových rolí (<c>ObsazeniProjektu</c>) i subsystémových rolí
/// (<c>ObsazeniSubsystemuProjektu</c>) pro nadřazený projekt daného subsystému.
/// Subsystémově scopovaná oprávnění jsou tedy dostupná i při project-level dotazu
/// (projektId bez subsystemId), což umožňuje VEDOUCI_SUBSYSTEMU a příbuzným rolím
/// přístup k project-level features (MEDIUM-1 fix, 2026-04-22).
/// </remarks>
public sealed record AuthorizationSnapshot(
    bool IsSuperAdmin,
    IReadOnlySet<string> GlobalPermissions,
    IReadOnlyDictionary<int, IReadOnlySet<string>> PerProjectPermissions,
    IReadOnlyDictionary<int, IReadOnlySet<string>> PerSubsystemPermissions)
{
    /// <summary>
    /// Vrátí true, pokud osoba má dané oprávnění v daném kontextu.
    /// </summary>
    /// <param name="key">Permission key (např. <c>records.edit</c>).</param>
    /// <param name="projektId">Projekt kontext, pokud je key project-scope.</param>
    /// <param name="subsystemId">Subsystem kontext, pokud je key subsystem-scope.</param>
    public bool HasPermission(string key, int? projektId = null, int? subsystemId = null)
    {
        if (IsSuperAdmin)
        {
            return true;
        }

        if (GlobalPermissions.Contains(key))
        {
            return true;
        }

        if (projektId.HasValue
            && PerProjectPermissions.TryGetValue(projektId.Value, out var projectPerms)
            && projectPerms.Contains(key))
        {
            return true;
        }

        if (subsystemId.HasValue
            && PerSubsystemPermissions.TryGetValue(subsystemId.Value, out var subsystemPerms)
            && subsystemPerms.Contains(key))
        {
            return true;
        }

        return false;
    }
}
