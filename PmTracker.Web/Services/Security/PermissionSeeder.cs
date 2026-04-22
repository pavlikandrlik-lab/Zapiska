using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Security;

public sealed class PermissionSeeder(PmTrackerDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await UpsertCategoriesAsync(ct);
        await UpsertRolesAsync(ct);
        await UpsertPermissionsAsync(ct);
        await dbContext.SaveChangesAsync(ct);

        var rolesByKod = await dbContext.AuthzRoles
            .AsNoTracking()
            .ToDictionaryAsync(role => role.Kod, StringComparer.OrdinalIgnoreCase, ct);
        var permissionsByKey = await dbContext.AuthzPermissions
            .AsNoTracking()
            .ToDictionaryAsync(permission => permission.Klic, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var mapping in PermissionSeedConfiguration.RoleMappings)
        {
            if (!rolesByKod.TryGetValue(mapping.RoleKod, out var role))
            {
                throw new InvalidOperationException($"Seeder role '{mapping.RoleKod}' nebyla nalezena.");
            }

            if (!permissionsByKey.TryGetValue(mapping.ActionKlic, out var permission))
            {
                throw new InvalidOperationException($"Seeder permission '{mapping.ActionKlic}' nebyla nalezena.");
            }

            var existing = await dbContext.AuthzRolePermissions
                .FirstOrDefaultAsync(
                    row => row.RoleId == role.Id && row.PermissionId == permission.Id,
                    ct);

            if (existing is null)
            {
                dbContext.AuthzRolePermissions.Add(new AuthzRolePermissionEntity
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    ScopeMode = mapping.ScopeMode,
                    IsAllowed = mapping.IsAllowed
                });
                continue;
            }

            existing.ScopeMode = mapping.ScopeMode;
            existing.IsAllowed = mapping.IsAllowed;
        }

        await dbContext.SaveChangesAsync(ct);

        // Authorization unification — Fáze A — Task A7: propojit lookup role s authz.roles přes FK
        var linker = new RoleCatalogLinker(dbContext);
        await linker.LinkAsync(ct);
    }

    private async Task UpsertCategoriesAsync(CancellationToken ct)
    {
        foreach (var category in PermissionSeedConfiguration.Categories)
        {
            var existing = await dbContext.AuthzPermissionCategories
                .FirstOrDefaultAsync(row => row.Kod == category.Kod, ct);

            if (existing is null)
            {
                dbContext.AuthzPermissionCategories.Add(new AuthzPermissionCategoryEntity
                {
                    Kod = category.Kod,
                    Nazev = category.Nazev,
                    SortOrder = category.SortOrder,
                    IsActive = true
                });
                continue;
            }

            existing.Nazev = category.Nazev;
            existing.SortOrder = category.SortOrder;
            existing.IsActive = true;
        }
    }

    private async Task UpsertRolesAsync(CancellationToken ct)
    {
        foreach (var role in PermissionSeedConfiguration.Roles)
        {
            var existing = await dbContext.AuthzRoles
                .FirstOrDefaultAsync(row => row.Kod == role.Kod, ct);

            if (existing is null)
            {
                dbContext.AuthzRoles.Add(new AuthzRoleEntity
                {
                    Kod = role.Kod,
                    Nazev = role.Nazev,
                    Popis = role.Popis,
                    IsSystem = role.IsSystem,
                    IsActive = true,
                    Scope = role.Scope
                });
                continue;
            }

            existing.Nazev = role.Nazev;
            existing.Popis = role.Popis;
            existing.IsSystem = role.IsSystem;
            existing.IsActive = true;
            existing.Scope = role.Scope;
        }
    }

    private async Task UpsertPermissionsAsync(CancellationToken ct)
    {
        var categoriesByKod = await dbContext.AuthzPermissionCategories
            .AsNoTracking()
            .ToDictionaryAsync(row => row.Kod, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var action in PermissionSeedConfiguration.Actions)
        {
            if (!categoriesByKod.TryGetValue(action.CategoryKod, out var category))
            {
                throw new InvalidOperationException($"Seeder category '{action.CategoryKod}' nebyla nalezena.");
            }

            var existing = await dbContext.AuthzPermissions
                .FirstOrDefaultAsync(row => row.Klic == action.Klic, ct);

            if (existing is null)
            {
                dbContext.AuthzPermissions.Add(new AuthzPermissionEntity
                {
                    Klic = action.Klic,
                    Nazev = action.Nazev,
                    CategoryId = category.Id,
                    ScopeLevel = action.ScopeLevel,
                    IsSystem = true,
                    IsActive = true
                });
                continue;
            }

            existing.Nazev = action.Nazev;
            existing.CategoryId = category.Id;
            existing.ScopeLevel = action.ScopeLevel;
            existing.IsSystem = true;
            existing.IsActive = true;
        }
    }
}
