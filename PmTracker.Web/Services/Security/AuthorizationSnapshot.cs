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
/// <param name="PerProjectDirectPermissions">
/// Oprávnění získaná VÝHRADNĚ přímou projektovou rolí (<c>ObsazeniProjektu</c>), bez příspěvku
/// subsystémových rolí. Na rozdíl od <see cref="PerProjectPermissions"/> sem NEpatří granty
/// propagované z <c>ObsazeniSubsystemuProjektu</c> (MEDIUM-1). Slouží k odlišení „mám klíč díky
/// projektové roli" (platí celoprojektově) od „mám ho jen jako vedoucí subsystému"
/// (musí být omezeno na vlastní subsystém — viz <see cref="HasDirectProjectPermission"/>).
/// <c>null</c> = neuvedeno; <see cref="HasDirectProjectPermission"/> pak degraduje na
/// <see cref="PerProjectPermissions"/> (zpětná kompatibilita pro volání bez tohoto rozlišení).
/// </param>
public sealed record AuthorizationSnapshot(
    bool IsSuperAdmin,
    IReadOnlySet<string> GlobalPermissions,
    IReadOnlyDictionary<int, IReadOnlySet<string>> PerProjectPermissions,
    IReadOnlyDictionary<int, IReadOnlySet<string>> PerSubsystemPermissions,
    IReadOnlyDictionary<int, IReadOnlySet<string>>? PerProjectDirectPermissions = null)
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

    /// <summary>
    /// Vrátí true, pokud osoba má dané oprávnění z PŘÍMÉ projektové role (nebo globálně /
    /// jako SuperAdmin), tj. NE pouze ze zděděného subsystémového grantu (MEDIUM-1).
    /// Pokud <see cref="PerProjectDirectPermissions"/> není vyplněno, degraduje na
    /// <see cref="HasPermission(string, int?, int?)"/> nad <see cref="PerProjectPermissions"/>.
    /// </summary>
    public bool HasDirectProjectPermission(string key, int projektId)
    {
        if (IsSuperAdmin)
        {
            return true;
        }

        if (GlobalPermissions.Contains(key))
        {
            return true;
        }

        if (PerProjectDirectPermissions is null)
        {
            return PerProjectPermissions.TryGetValue(projektId, out var effective) && effective.Contains(key);
        }

        return PerProjectDirectPermissions.TryGetValue(projektId, out var direct) && direct.Contains(key);
    }
}
