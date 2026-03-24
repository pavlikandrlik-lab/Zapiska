using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Settings;

public sealed class SettingsAuthzCommands(
    PmTrackerDbContext dbContext,
    TimeProvider timeProvider) : ISettingsAuthzCommands
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public async Task SaveUserRoleAssignmentAsync(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!await dbContext.Osoby.AsNoTracking().AnyAsync(x => x.Id == command.OsobaId, ct))
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }

        var role = await dbContext.AuthzRoles.FirstOrDefaultAsync(x => x.Id == command.RoleId, ct);
        if (role is null)
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        var row = await dbContext.AuthzUserRoles.FirstOrDefaultAsync(
            x => x.OsobaId == command.OsobaId && x.RoleId == command.RoleId,
            ct);
        var oldValue = row is null ? null : JsonSerializer.Serialize(row);
        if (row is null)
        {
            row = new AuthzUserRoleEntity
            {
                OsobaId = command.OsobaId,
                RoleId = command.RoleId,
                IsActive = command.IsActive,
                CreatedAt = GetUtcNow()
            };
            dbContext.AuthzUserRoles.Add(row);
        }
        else
        {
            row.IsActive = command.IsActive;
        }

        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "authz.user_roles", row.Id.ToString(CultureInfo.InvariantCulture), "upsert", oldValue, JsonSerializer.Serialize(row), ct);
    }

    public async Task SaveUserRolesForUserAsync(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!await dbContext.Osoby.AsNoTracking().AnyAsync(x => x.Id == command.OsobaId, ct))
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }

        var requestedRoleIds = (command.RoleIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var roleRows = await dbContext.AuthzRoles.AsNoTracking()
            .Where(x => requestedRoleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.IsActive })
            .ToListAsync(ct);

        if (roleRows.Count != requestedRoleIds.Count)
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        if (roleRows.Any(x => !x.IsActive))
        {
            throw new InvalidOperationException("Nelze přiřadit neaktivní roli.");
        }

        var oldRows = await dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == command.OsobaId)
            .OrderBy(x => x.RoleId)
            .ToListAsync(ct);

        var existingRows = await dbContext.AuthzUserRoles
            .Where(x => x.OsobaId == command.OsobaId)
            .ToListAsync(ct);

        var requestedSet = requestedRoleIds.ToHashSet();
        var existingByRoleId = existingRows.ToDictionary(x => x.RoleId);
        foreach (var existing in existingRows)
        {
            existing.IsActive = requestedSet.Contains(existing.RoleId);
        }

        foreach (var roleId in requestedRoleIds)
        {
            if (existingByRoleId.ContainsKey(roleId))
            {
                continue;
            }

            dbContext.AuthzUserRoles.Add(new AuthzUserRoleEntity
            {
                OsobaId = command.OsobaId,
                RoleId = roleId,
                IsActive = true,
                CreatedAt = GetUtcNow()
            });
        }

        await dbContext.SaveChangesAsync(ct);

        var newRows = await dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == command.OsobaId)
            .OrderBy(x => x.RoleId)
            .ToListAsync(ct);

        await WriteAuditAsync(
            currentUser.OsobaId,
            "authz.user_roles",
            command.OsobaId.ToString(CultureInfo.InvariantCulture),
            "replace",
            JsonSerializer.Serialize(oldRows),
            JsonSerializer.Serialize(newRows),
            ct);
    }

    public async Task SaveAuthzRoleAsync(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var kod = command.Kod.Trim();
        var nazev = command.Nazev.Trim();
        var popis = string.IsNullOrWhiteSpace(command.Popis) ? null : command.Popis.Trim();
        if (string.IsNullOrWhiteSpace(kod) || string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Vyplňte kód i název role.");
        }

        var existingRoleCodes = await dbContext.AuthzRoles.AsNoTracking()
            .Where(x => !command.Id.HasValue || x.Id != command.Id.Value)
            .Select(x => x.Kod)
            .ToListAsync(ct);
        var duplicateExists = existingRoleCodes.Any(existingCode => Ci.Equals(existingCode, kod));
        if (duplicateExists)
        {
            throw new InvalidOperationException($"Role s kódem '{kod}' už existuje.");
        }

        AuthzRoleEntity role;
        string action;
        string? oldValue = null;

        if (command.Id is int roleId)
        {
            role = await dbContext.AuthzRoles.FirstOrDefaultAsync(x => x.Id == roleId, ct)
                ?? throw new InvalidOperationException("Role nebyla nalezena.");

            if (role.IsSystem)
            {
                throw new InvalidOperationException("Systémovou roli nelze upravit.");
            }

            oldValue = JsonSerializer.Serialize(role);
            role.Kod = kod;
            role.Nazev = nazev;
            role.Popis = popis;
            action = "update";
        }
        else
        {
            role = new AuthzRoleEntity
            {
                Kod = kod,
                Nazev = nazev,
                Popis = popis,
                IsSystem = false,
                IsActive = true
            };
            dbContext.AuthzRoles.Add(role);
            action = "create";
        }

        await dbContext.SaveChangesAsync(ct);

        await WriteAuditAsync(
            currentUser.OsobaId,
            "authz.roles",
            role.Id.ToString(CultureInfo.InvariantCulture),
            action,
            oldValue,
            JsonSerializer.Serialize(role),
            ct);
    }

    public async Task ToggleAuthzRoleAsync(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var role = await dbContext.AuthzRoles.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new InvalidOperationException("Role nebyla nalezena.");

        if (role.IsSystem)
        {
            throw new InvalidOperationException("Systémovou roli nelze deaktivovat.");
        }

        var oldValue = JsonSerializer.Serialize(role);
        role.IsActive = command.IsActive;
        await dbContext.SaveChangesAsync(ct);

        await WriteAuditAsync(
            currentUser.OsobaId,
            "authz.roles",
            role.Id.ToString(CultureInfo.InvariantCulture),
            command.IsActive ? "activate" : "deactivate",
            oldValue,
            JsonSerializer.Serialize(role),
            ct);
    }

    public async Task SaveAuthzPermissionAsync(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var klic = command.Klic.Trim();
        var nazev = command.Nazev.Trim();
        var scopeLevel = (command.ScopeLevel ?? string.Empty).Trim().ToUpperInvariant();
        var categoryId = command.CategoryId;
        if (string.IsNullOrWhiteSpace(klic) || string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Vyplňte klíč i název akce.");
        }

        if (!PermissionKeys.IsSupported(klic))
        {
            throw new InvalidOperationException($"Klíč '{klic}' není v seznamu podporovaných akcí. Vyberte klíč z nabídky.");
        }

        var catalogEntry = PermissionKeys.BuildCatalog().FirstOrDefault(x => Ci.Equals(x.Key, klic));
        if (catalogEntry is not null)
        {
            var categoryCode = catalogEntry.CategoryKod.Trim();
            var mappedCategoryId = await dbContext.AuthzPermissionCategories.AsNoTracking()
                .Where(x => x.IsActive && x.Kod == categoryCode)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(ct);

            if (!mappedCategoryId.HasValue)
            {
                throw new InvalidOperationException($"Katalog akcí odkazuje na neexistující kategorii '{categoryCode}'.");
            }

            categoryId = mappedCategoryId.Value;
            scopeLevel = catalogEntry.ScopeLevel;
        }

        if (!Ci.Equals(scopeLevel, "GLOBAL") && !Ci.Equals(scopeLevel, "PROJECT"))
        {
            throw new InvalidOperationException("Neplatný rozsah akce.");
        }

        if (!await dbContext.AuthzPermissionCategories.AsNoTracking().AnyAsync(x => x.Id == categoryId && x.IsActive, ct))
        {
            throw new InvalidOperationException("Vybraná kategorie akcí neexistuje.");
        }

        var existingPermissionKeys = await dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => !command.Id.HasValue || x.Id != command.Id.Value)
            .Select(x => x.Klic)
            .ToListAsync(ct);
        var duplicateExists = existingPermissionKeys.Any(existingKey => Ci.Equals(existingKey, klic));
        if (duplicateExists)
        {
            throw new InvalidOperationException($"Akce s klíčem '{klic}' už existuje.");
        }

        AuthzPermissionEntity permission;
        string action;
        string? oldValue = null;

        if (command.Id is int permissionId)
        {
            permission = await dbContext.AuthzPermissions.FirstOrDefaultAsync(x => x.Id == permissionId, ct)
                ?? throw new InvalidOperationException("Akce nebyla nalezena.");

            if (permission.IsSystem)
            {
                throw new InvalidOperationException("Systémovou akci nelze upravit.");
            }

            oldValue = JsonSerializer.Serialize(permission);
            permission.Klic = klic;
            permission.Nazev = nazev;
            permission.CategoryId = categoryId;
            permission.ScopeLevel = scopeLevel;
            action = "update";
        }
        else
        {
            permission = new AuthzPermissionEntity
            {
                Klic = klic,
                Nazev = nazev,
                CategoryId = categoryId,
                ScopeLevel = scopeLevel,
                IsActive = true,
                IsSystem = false
            };
            dbContext.AuthzPermissions.Add(permission);
            action = "create";
        }

        await dbContext.SaveChangesAsync(ct);

        await WriteAuditAsync(
            currentUser.OsobaId,
            "authz.permissions",
            permission.Id.ToString(CultureInfo.InvariantCulture),
            action,
            oldValue,
            JsonSerializer.Serialize(permission),
            ct);
    }

    public async Task ToggleAuthzPermissionAsync(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var permission = await dbContext.AuthzPermissions.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new InvalidOperationException("Akce nebyla nalezena.");

        if (permission.IsSystem)
        {
            throw new InvalidOperationException("Systémovou akci nelze deaktivovat.");
        }

        var oldValue = JsonSerializer.Serialize(permission);
        permission.IsActive = command.IsActive;
        await dbContext.SaveChangesAsync(ct);

        await WriteAuditAsync(
            currentUser.OsobaId,
            "authz.permissions",
            permission.Id.ToString(CultureInfo.InvariantCulture),
            command.IsActive ? "activate" : "deactivate",
            oldValue,
            JsonSerializer.Serialize(permission),
            ct);
    }

    public async Task SaveRolePermissionAsync(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var scopeMode = string.Equals(command.ScopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase)
            ? "INCLUDE"
            : string.Equals(command.ScopeMode, "ALL", StringComparison.OrdinalIgnoreCase)
                ? "ALL"
                : throw new InvalidOperationException("Neplatný rozsah mapování role/akce.");

        if (!await dbContext.AuthzRoles.AsNoTracking().AnyAsync(x => x.Id == command.RoleId, ct))
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        if (!await dbContext.AuthzPermissions.AsNoTracking().AnyAsync(x => x.Id == command.PermissionId, ct))
        {
            throw new InvalidOperationException("Vybraná akce neexistuje.");
        }

        AuthzRolePermissionEntity? row = null;
        if (command.Id.HasValue)
        {
            row = await dbContext.AuthzRolePermissions.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException("Mapování role/akce nebylo nalezeno.");
        }
        else
        {
            row = await dbContext.AuthzRolePermissions.FirstOrDefaultAsync(
                x => x.RoleId == command.RoleId && x.PermissionId == command.PermissionId,
                ct);
        }

        var currentRolePermissionId = row?.Id;
        var duplicateExists = await dbContext.AuthzRolePermissions.AsNoTracking()
            .AnyAsync(x => x.RoleId == command.RoleId
                           && x.PermissionId == command.PermissionId
                           && (!currentRolePermissionId.HasValue || x.Id != currentRolePermissionId.Value), ct);
        if (duplicateExists)
        {
            throw new InvalidOperationException("Pro zvolenou roli a akci už mapování existuje.");
        }

        var oldValue = row is null ? null : JsonSerializer.Serialize(row);
        if (row is null)
        {
            row = new AuthzRolePermissionEntity
            {
                RoleId = command.RoleId,
                PermissionId = command.PermissionId,
                ScopeMode = scopeMode,
                IsAllowed = command.IsAllowed
            };
            dbContext.AuthzRolePermissions.Add(row);
        }
        else
        {
            row.RoleId = command.RoleId;
            row.PermissionId = command.PermissionId;
            row.ScopeMode = scopeMode;
            row.IsAllowed = command.IsAllowed;
        }

        await dbContext.SaveChangesAsync(ct);

        var currentProjects = await dbContext.AuthzRolePermissionProjects
            .Where(x => x.RolePermissionId == row.Id)
            .ToListAsync(ct);
        dbContext.AuthzRolePermissionProjects.RemoveRange(currentProjects);
        if (scopeMode == "INCLUDE")
        {
            var projectIds = (command.ProjektIds ?? [])
                .Where(x => x > 0)
                .Distinct()
                .ToList();
            var validProjectIdRows = await dbContext.Projekty.AsNoTracking()
                .Where(x => projectIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(ct);
            var validProjectIds = validProjectIdRows.ToHashSet();
            var missingProjectIds = projectIds.Where(x => !validProjectIds.Contains(x)).ToList();
            if (missingProjectIds.Count > 0)
            {
                throw new InvalidOperationException("Vybrané projekty pro INCLUDE mapování neexistují.");
            }

            foreach (var projectId in projectIds)
            {
                dbContext.AuthzRolePermissionProjects.Add(new AuthzRolePermissionProjectEntity
                {
                    RolePermissionId = row.Id,
                    ProjektId = projectId
                });
            }
        }

        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "authz.role_permissions", row.Id.ToString(CultureInfo.InvariantCulture), "upsert", oldValue, JsonSerializer.Serialize(command), ct);
    }

    public async Task DeleteRolePermissionAsync(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var row = await dbContext.AuthzRolePermissions.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new InvalidOperationException("Mapování role/akce nebylo nalezeno.");

        var oldValue = JsonSerializer.Serialize(row);
        var linkedProjects = await dbContext.AuthzRolePermissionProjects
            .Where(x => x.RolePermissionId == command.Id)
            .ToListAsync(ct);

        if (linkedProjects.Count > 0)
        {
            dbContext.AuthzRolePermissionProjects.RemoveRange(linkedProjects);
        }

        dbContext.AuthzRolePermissions.Remove(row);
        await dbContext.SaveChangesAsync(ct);

        await WriteAuditAsync(
            currentUser.OsobaId,
            "authz.role_permissions",
            command.Id.ToString(CultureInfo.InvariantCulture),
            "delete",
            oldValue,
            JsonSerializer.Serialize(command),
            ct);
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private async Task WriteAuditAsync(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue, CancellationToken ct)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = GetUtcNow()
        });
        await dbContext.SaveChangesAsync(ct);
    }
}
