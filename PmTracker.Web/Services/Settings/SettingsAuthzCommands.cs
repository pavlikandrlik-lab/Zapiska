using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Services.Settings;

public sealed class SettingsAuthzCommands(
    PmTrackerDbContext dbContext,
    IAuditWriteService auditWriteService,
    TimeProvider timeProvider) : ISettingsAuthzCommands
{
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

        if (role.Scope != RoleScope.Global)
        {
            throw new InvalidOperationException("Only Global-scope roles can be assigned via runtime UI. Project/subsystem roles are managed via seed.");
        }

        var row = await dbContext.AuthzUserRoles.FirstOrDefaultAsync(
            x => x.OsobaId == command.OsobaId && x.RoleId == command.RoleId,
            ct);
        var oldSnapshot = row is null ? null : AuthzUserRoleAuditSnapshot.FromEntity(row);
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
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            oldSnapshot is null
                ? AuditActionType.Create
                : oldSnapshot.IsActive == row.IsActive
                    ? AuditActionType.Update
                    : row.IsActive
                        ? AuditActionType.Activate
                        : AuditActionType.Deactivate,
            AuditEntityType.AuthzUserRole,
            row.Id.ToString(CultureInfo.InvariantCulture),
            oldSnapshot,
            AuthzUserRoleAuditSnapshot.FromEntity(row)));
        await dbContext.SaveChangesAsync(ct);
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
            .Select(x => new { x.Id, x.IsActive, x.Scope })
            .ToListAsync(ct);

        if (roleRows.Count != requestedRoleIds.Count)
        {
            throw new InvalidOperationException("Vybraná role neexistuje.");
        }

        if (roleRows.Any(x => x.Scope != RoleScope.Global))
        {
            throw new InvalidOperationException("Only Global-scope roles can be assigned via runtime UI. Project/subsystem roles are managed via seed.");
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

        var oldByRoleId = oldRows.ToDictionary(x => x.RoleId);
        foreach (var newRow in newRows)
        {
            oldByRoleId.TryGetValue(newRow.RoleId, out var oldRow);
            if (oldRow is not null && oldRow.IsActive == newRow.IsActive)
            {
                continue;
            }

            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                oldRow is null
                    ? AuditActionType.Create
                    : oldRow.IsActive == newRow.IsActive
                        ? AuditActionType.Update
                        : newRow.IsActive
                            ? AuditActionType.Activate
                            : AuditActionType.Deactivate,
                AuditEntityType.AuthzUserRole,
                newRow.Id.ToString(CultureInfo.InvariantCulture),
                oldRow is null ? null : AuthzUserRoleAuditSnapshot.FromEntity(oldRow),
                AuthzUserRoleAuditSnapshot.FromEntity(newRow)));
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private DateTime GetUtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
