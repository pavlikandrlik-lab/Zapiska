using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

/// <summary>
/// Staví <see cref="AuthorizationSnapshot"/> z DB pro danou osobu. Používá se z
/// <see cref="AuthorizationService"/>. Pracuje se stejným DB modelem jako
/// <c>UserContextResolver.LoadDbDriven*GrantsAsync</c> (Fáze B), ale produkuje
/// strukturované sety místo VM grantů.
/// </summary>
internal sealed class AuthorizationSnapshotBuilder(PmTrackerDbContext db)
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

        var perProject = projectRows
            .GroupBy(x => x.ProjektId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(x => x.Klic), StringComparer.OrdinalIgnoreCase));

        // 4. Per-subsystem permissions — via ObsazeniSubsystemuProjektu × CiselnikRoliSubsystemu.AuthzRoleId.
        //    Klíčem je ProjektSubsystemId (z přiřazení), filtrace: aktivní ProjektSubsystemy.
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
                select new { a.ProjektSubsystemId, p.Klic })
            .ToListAsync(ct);

        var perSubsystem = subsystemRows
            .GroupBy(x => x.ProjektSubsystemId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)new HashSet<string>(g.Select(x => x.Klic), StringComparer.OrdinalIgnoreCase));

        return new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(globalPerms, StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: perProject,
            PerSubsystemPermissions: perSubsystem);
    }
}
