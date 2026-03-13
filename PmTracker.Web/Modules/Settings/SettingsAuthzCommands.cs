using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Settings;

public sealed class SettingsAuthzCommands(
    PmTrackerDbContext dbContext,
    TimeProvider timeProvider) : ISettingsAuthzCommands
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public void SaveUserRoleAssignment(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!dbContext.Osoby.AsNoTracking().Any(x => x.Id == command.OsobaId))
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }

        var role = dbContext.AuthzRoles.FirstOrDefault(x => x.Id == command.RoleId);
        if (role is null)
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        var row = dbContext.AuthzUserRoles.FirstOrDefault(x => x.OsobaId == command.OsobaId && x.RoleId == command.RoleId);
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

        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "authz.user_roles", row.Id.ToString(CultureInfo.InvariantCulture), "upsert", oldValue, JsonSerializer.Serialize(row));
    }

    public void SaveUserRolesForUser(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!dbContext.Osoby.AsNoTracking().Any(x => x.Id == command.OsobaId))
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }

        var requestedRoleIds = (command.RoleIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var roleRows = dbContext.AuthzRoles.AsNoTracking()
            .Where(x => requestedRoleIds.Contains(x.Id))
            .Select(x => new { x.Id, x.IsActive })
            .ToList();

        if (roleRows.Count != requestedRoleIds.Count)
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        if (roleRows.Any(x => !x.IsActive))
        {
            throw new InvalidOperationException("Nelze přiřadit neaktivní roli.");
        }

        var oldRows = dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == command.OsobaId)
            .OrderBy(x => x.RoleId)
            .ToList();

        var existingRows = dbContext.AuthzUserRoles
            .Where(x => x.OsobaId == command.OsobaId)
            .ToList();

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

        dbContext.SaveChanges();

        var newRows = dbContext.AuthzUserRoles.AsNoTracking()
            .Where(x => x.OsobaId == command.OsobaId)
            .OrderBy(x => x.RoleId)
            .ToList();

        WriteAudit(
            currentUser.OsobaId,
            "authz.user_roles",
            command.OsobaId.ToString(CultureInfo.InvariantCulture),
            "replace",
            JsonSerializer.Serialize(oldRows),
            JsonSerializer.Serialize(newRows));
    }

    public void SaveAuthzRole(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        var kod = command.Kod.Trim();
        var nazev = command.Nazev.Trim();
        var popis = string.IsNullOrWhiteSpace(command.Popis) ? null : command.Popis.Trim();
        if (string.IsNullOrWhiteSpace(kod) || string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Vyplňte kód i název role.");
        }

        var duplicateExists = dbContext.AuthzRoles.AsNoTracking()
            .Where(x => !command.Id.HasValue || x.Id != command.Id.Value)
            .Select(x => x.Kod)
            .ToList()
            .Any(existingCode => Ci.Equals(existingCode, kod));
        if (duplicateExists)
        {
            throw new InvalidOperationException($"Role s kódem '{kod}' už existuje.");
        }

        AuthzRoleEntity role;
        string action;
        string? oldValue = null;

        if (command.Id is int roleId)
        {
            role = dbContext.AuthzRoles.FirstOrDefault(x => x.Id == roleId)
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

        dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.roles",
            role.Id.ToString(CultureInfo.InvariantCulture),
            action,
            oldValue,
            JsonSerializer.Serialize(role));
    }

    public void ToggleAuthzRole(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        var role = dbContext.AuthzRoles.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException("Role nebyla nalezena.");

        if (role.IsSystem)
        {
            throw new InvalidOperationException("Systémovou roli nelze deaktivovat.");
        }

        var oldValue = JsonSerializer.Serialize(role);
        role.IsActive = command.IsActive;
        dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.roles",
            role.Id.ToString(CultureInfo.InvariantCulture),
            command.IsActive ? "activate" : "deactivate",
            oldValue,
            JsonSerializer.Serialize(role));
    }

    public void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser)
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
            var mappedCategoryId = dbContext.AuthzPermissionCategories.AsNoTracking()
                .Where(x => x.IsActive && x.Kod == categoryCode)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();

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

        if (!dbContext.AuthzPermissionCategories.AsNoTracking().Any(x => x.Id == categoryId && x.IsActive))
        {
            throw new InvalidOperationException("Vybraná kategorie akcí neexistuje.");
        }

        var duplicateExists = dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => !command.Id.HasValue || x.Id != command.Id.Value)
            .Select(x => x.Klic)
            .ToList()
            .Any(existingKey => Ci.Equals(existingKey, klic));
        if (duplicateExists)
        {
            throw new InvalidOperationException($"Akce s klíčem '{klic}' už existuje.");
        }

        AuthzPermissionEntity permission;
        string action;
        string? oldValue = null;

        if (command.Id is int permissionId)
        {
            permission = dbContext.AuthzPermissions.FirstOrDefault(x => x.Id == permissionId)
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

        dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.permissions",
            permission.Id.ToString(CultureInfo.InvariantCulture),
            action,
            oldValue,
            JsonSerializer.Serialize(permission));
    }

    public void ToggleAuthzPermission(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser)
    {
        var permission = dbContext.AuthzPermissions.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException("Akce nebyla nalezena.");

        if (permission.IsSystem)
        {
            throw new InvalidOperationException("Systémovou akci nelze deaktivovat.");
        }

        var oldValue = JsonSerializer.Serialize(permission);
        permission.IsActive = command.IsActive;
        dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.permissions",
            permission.Id.ToString(CultureInfo.InvariantCulture),
            command.IsActive ? "activate" : "deactivate",
            oldValue,
            JsonSerializer.Serialize(permission));
    }

    public void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser)
    {
        var scopeMode = string.Equals(command.ScopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase)
            ? "INCLUDE"
            : string.Equals(command.ScopeMode, "ALL", StringComparison.OrdinalIgnoreCase)
                ? "ALL"
                : throw new InvalidOperationException("Neplatný rozsah mapování role/akce.");

        if (!dbContext.AuthzRoles.AsNoTracking().Any(x => x.Id == command.RoleId))
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        if (!dbContext.AuthzPermissions.AsNoTracking().Any(x => x.Id == command.PermissionId))
        {
            throw new InvalidOperationException("Vybraná akce neexistuje.");
        }

        AuthzRolePermissionEntity? row = null;
        if (command.Id.HasValue)
        {
            row = dbContext.AuthzRolePermissions.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException("Mapování role/akce nebylo nalezeno.");
        }
        else
        {
            row = dbContext.AuthzRolePermissions.FirstOrDefault(x => x.RoleId == command.RoleId && x.PermissionId == command.PermissionId);
        }

        var currentRolePermissionId = row?.Id;
        var duplicateExists = dbContext.AuthzRolePermissions.AsNoTracking()
            .Any(x => x.RoleId == command.RoleId
                      && x.PermissionId == command.PermissionId
                      && (!currentRolePermissionId.HasValue || x.Id != currentRolePermissionId.Value));
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

        dbContext.SaveChanges();

        var currentProjects = dbContext.AuthzRolePermissionProjects.Where(x => x.RolePermissionId == row.Id).ToList();
        dbContext.AuthzRolePermissionProjects.RemoveRange(currentProjects);
        if (scopeMode == "INCLUDE")
        {
            var projectIds = (command.ProjektIds ?? [])
                .Where(x => x > 0)
                .Distinct()
                .ToList();
            var validProjectIds = dbContext.Projekty.AsNoTracking()
                .Where(x => projectIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToHashSet();
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

        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "authz.role_permissions", row.Id.ToString(CultureInfo.InvariantCulture), "upsert", oldValue, JsonSerializer.Serialize(command));
    }

    public void DeleteRolePermission(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser)
    {
        var row = dbContext.AuthzRolePermissions.FirstOrDefault(x => x.Id == command.Id)
            ?? throw new InvalidOperationException("Mapování role/akce nebylo nalezeno.");

        var oldValue = JsonSerializer.Serialize(row);
        var linkedProjects = dbContext.AuthzRolePermissionProjects
            .Where(x => x.RolePermissionId == command.Id)
            .ToList();

        if (linkedProjects.Count > 0)
        {
            dbContext.AuthzRolePermissionProjects.RemoveRange(linkedProjects);
        }

        dbContext.AuthzRolePermissions.Remove(row);
        dbContext.SaveChanges();

        WriteAudit(
            currentUser.OsobaId,
            "authz.role_permissions",
            command.Id.ToString(CultureInfo.InvariantCulture),
            "delete",
            oldValue,
            JsonSerializer.Serialize(command));
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private void WriteAudit(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue)
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
        dbContext.SaveChanges();
    }
}
