using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// Staví <see cref="AuthorizationSnapshot"/> z DB pro danou osobu. Používá se z
/// <see cref="AuthorizationService"/>. Pracuje se stejným DB modelem jako
/// <c>UserContextResolver.LoadDbDriven*GrantsAsync</c> (Fáze B), ale produkuje
/// strukturované sety místo VM grantů.
/// </summary>
/// <remarks>
/// MEDIUM-1 fix (2026-04-22): subsystémově scopovaná oprávnění jsou automaticky propagována
/// i do <c>PerProjectPermissions</c> pro nadřazený projekt subsystému. To znamená, že role
/// jako <c>VEDOUCI_SUBSYSTEMU</c>, <c>ZASTUPCE_VEDOUCIHO_SUBSYSTEMU</c> nebo
/// <c>METODIK_SUBSYSTEMU</c> umožňují přístup k project-level features (dashboard, seznam
/// záznamů, export) bez nutnosti explicitního přiřazení projektové role. Viz
/// <see cref="AuthorizationSnapshot"/> pro sémantiku <c>PerProjectPermissions</c>.
/// </remarks>
internal sealed class AuthorizationSnapshotBuilder(PmTrackerDbContext db) : IAuthorizationSnapshotBuilder
{
    public async Task<AuthorizationSnapshot> BuildAsync(int osobaId, CancellationToken ct)
    {
        // 1. SuperAdmin status — AuthzSuperadmins tabulka (bez IsActive, entita ho nemá).
        var isSuperAdmin = await db.AuthzSuperadmins
            .AsNoTracking()
            .AnyAsync(s => s.OsobaId == osobaId, ct);

        // 2. Global permissions — přiřazené role v AuthzUserRoles → AuthzRolePermissions → AuthzPermissions.
        var globalPerms = await (
                from ur in db.AuthzUserRoles.AsNoTracking()
                where ur.OsobaId == osobaId && ur.IsActive
                join r in db.AuthzRoles.AsNoTracking() on ur.RoleId equals r.Id
                where r.IsActive
                join rp in db.AuthzRolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                where rp.IsAllowed
                join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                where p.IsActive
                select p.Klic)
            .Distinct()
            .ToListAsync(ct);

        // 3. Per-project permissions — via ObsazeniProjektu × CiselnikRoliProjektu.AuthzRoleId.
        var projectRows = await (
                from a in db.ObsazeniProjektu.AsNoTracking()
                where a.OsobaId == osobaId && !a.DatumOdebrani.HasValue
                join lr in db.CiselnikRoliProjektu.AsNoTracking() on a.RoleId equals lr.Id
                where lr.AuthzRoleId != null
                join r in db.AuthzRoles.AsNoTracking() on lr.AuthzRoleId equals r.Id
                where r.IsActive
                join rp in db.AuthzRolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                where rp.IsAllowed
                join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                where p.IsActive
                select new { a.ProjektId, p.Klic })
            .ToListAsync(ct);

        // 4. Per-subsystem permissions — via ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu.AuthzRoleId.
        //    Klíčem je ProjektSubsystemId (z přiřazení), filtrace: aktivní ProjektSubsystemy.
        //    MEDIUM-1 fix: přidáme ps.ProjektId do projekce, aby bylo možné propagovat do PerProject.
        var subsystemRows = await (
                from a in db.ObsazeniSubsystemuProjektu.AsNoTracking()
                where a.OsobaId == osobaId && !a.DatumOdebrani.HasValue
                join ps in db.ProjektSubsystemy.AsNoTracking() on a.ProjektSubsystemId equals ps.Id
                where !ps.DatumOdebrani.HasValue
                join lr in db.CiselnikRoliSubsystemu.AsNoTracking() on a.RoleSubsystemuId equals lr.Id
                where lr.AuthzRoleId != null
                join r in db.AuthzRoles.AsNoTracking() on lr.AuthzRoleId equals r.Id
                where r.IsActive
                join rp in db.AuthzRolePermissions.AsNoTracking() on r.Id equals rp.RoleId
                where rp.IsAllowed
                join p in db.AuthzPermissions.AsNoTracking() on rp.PermissionId equals p.Id
                where p.IsActive
                select new { ps.ProjektId, a.ProjektSubsystemId, p.Klic })
            .ToListAsync(ct);

        var perSubsystem = subsystemRows
            .GroupBy(x => x.ProjektSubsystemId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(x => x.Klic), StringComparer.OrdinalIgnoreCase));

        // MEDIUM-1 fix: subsystem-scoped role implikuje project-level přístup pro daný klíč.
        // Začínáme s mutable work dict z projektových řádků a přidáme příspěvky subsystémů.
        var perProjectMutable = new Dictionary<int, HashSet<string>>();

        foreach (var row in projectRows)
        {
            if (!perProjectMutable.TryGetValue(row.ProjektId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                perProjectMutable[row.ProjektId] = set;
            }
            set.Add(row.Klic);
        }

        foreach (var row in subsystemRows)
        {
            if (!perProjectMutable.TryGetValue(row.ProjektId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                perProjectMutable[row.ProjektId] = set;
            }
            set.Add(row.Klic);
        }

        var perProjectFinal = perProjectMutable.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)kvp.Value);

        // Direct = jen příspěvek PŘÍMÝCH projektových rolí (projectRows), BEZ MEDIUM-1 propagace
        // subsystémových grantů. Umožňuje odlišit „mám klíč díky projektové roli" (celoprojektově)
        // od „mám ho jen jako vedoucí subsystému" (omezeno na vlastní subsystém).
        var perProjectDirectMutable = new Dictionary<int, HashSet<string>>();
        foreach (var row in projectRows)
        {
            if (!perProjectDirectMutable.TryGetValue(row.ProjektId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                perProjectDirectMutable[row.ProjektId] = set;
            }
            set.Add(row.Klic);
        }

        var perProjectDirect = perProjectDirectMutable.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<string>)kvp.Value);

        return new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(globalPerms, StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: perProjectFinal,
            PerSubsystemPermissions: perSubsystem,
            PerProjectDirectPermissions: perProjectDirect);
    }
}
