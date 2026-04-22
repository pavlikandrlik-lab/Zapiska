using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;

namespace PmTracker.Web.Services.Security;

public sealed class RoleCatalogLinker(PmTrackerDbContext db)
{
    public async Task LinkAsync(CancellationToken cancellationToken)
    {
        var authzRolesByKod = await db.AuthzRoles
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Kod, r => r.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var projectRoles = await db.CiselnikRoliProjektu
            .Where(r => r.AuthzRoleId == null)
            .ToListAsync(cancellationToken);

        foreach (var row in projectRoles)
        {
            if (authzRolesByKod.TryGetValue(row.Kod, out var authzRoleId))
            {
                row.AuthzRoleId = authzRoleId;
            }
        }

        var subsystemRoles = await db.CiselnikRoliSubsystemu
            .Where(r => r.AuthzRoleId == null)
            .ToListAsync(cancellationToken);

        foreach (var row in subsystemRoles)
        {
            if (authzRolesByKod.TryGetValue(row.Kod, out var authzRoleId))
            {
                row.AuthzRoleId = authzRoleId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
