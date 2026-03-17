using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public sealed class ProjectAssignmentCommandsUseCase(
    PmTrackerDbContext dbContext) : IProjectAssignmentCommandsUseCase
{
    public void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var personId = command.OsobaId.Value;
        var role = dbContext.CiselnikRoliProjektu.FirstOrDefault(x => x.Kod == command.RoleKod || x.Nazev == command.RoleKod)
            ?? throw new InvalidOperationException($"Role projektu '{command.RoleKod}' nebyla nalezena.");
        EnsurePersonExists(personId);

        var activeRolesForPerson = dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == command.ProjektId && x.OsobaId == personId && !x.DatumOdebrani.HasValue)
            .ToList();
        var activeRoleIdsForPerson = activeRolesForPerson.Select(x => x.RoleId).ToHashSet();
        var hostRoleId = dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == ProjectRoleCodes.Host)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        var ownerRoleId = dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == ProjectRoleCodes.ProjectOwner)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

        if (activeRolesForPerson.Any(x => x.RoleId == role.Id))
        {
            throw new InvalidOperationException("Vybraná role je už osobě v projektu aktivně přiřazena.");
        }

        if (hostRoleId.HasValue && role.Id == hostRoleId.Value && activeRolesForPerson.Count > 0)
        {
            throw new InvalidOperationException("Role Host nesmí koexistovat s jinou aktivní projektovou rolí stejné osoby.");
        }

        if (hostRoleId.HasValue && role.Id == hostRoleId.Value && PersonHasActiveSubsystemRole(command.ProjektId, personId))
        {
            throw new InvalidOperationException("Osoba s aktivní rolí v subsystému nesmí mít projektovou roli Host.");
        }

        if (hostRoleId.HasValue && activeRoleIdsForPerson.Contains(hostRoleId.Value))
        {
            throw new InvalidOperationException("Osoba s aktivní rolí Host nesmí dostat další projektovou roli.");
        }

        if (ownerRoleId.HasValue && role.Id == ownerRoleId.Value)
        {
            var ownerExists = dbContext.ObsazeniProjektu.AsNoTracking()
                .Any(x => x.ProjektId == command.ProjektId && x.RoleId == ownerRoleId.Value && !x.DatumOdebrani.HasValue);
            if (ownerExists)
            {
                throw new InvalidOperationException("Projekt už má aktivního vlastníka projektu.");
            }
        }

        var entity = new ObsazeniProjektuEntity
        {
            ProjektId = command.ProjektId,
            OsobaId = personId,
            RoleId = role.Id,
            DatumPrirazeni = DateTime.UtcNow,
            DatumOdebrani = null
        };
        dbContext.ObsazeniProjektu.Add(entity);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(entity));
    }

    public void DeactivateProjectRole(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        var entity = dbContext.ObsazeniProjektu.FirstOrDefault(x => x.Id == command.ProjektRoleId)
            ?? throw new InvalidOperationException("Projektová role nebyla nalezena.");
        if (entity.DatumOdebrani.HasValue)
        {
            return;
        }

        var old = JsonSerializer.Serialize(entity);
        entity.DatumOdebrani = DateTime.UtcNow;
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "deactivate", old, JsonSerializer.Serialize(entity));
    }

    public void AssignProjectSubsystem(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
    {
        var subsystemId = ResolveSubsystemId(command.SubsystemKod);
        var exists = dbContext.ProjektSubsystemy.AsNoTracking()
            .Any(x => x.ProjektId == command.ProjektId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue);
        if (exists)
        {
            throw new InvalidOperationException("Subsystém je už projektu aktivně přiřazen.");
        }

        var entity = new ProjektSubsystemEntity
        {
            ProjektId = command.ProjektId,
            SubsystemId = subsystemId,
            DatumPrirazeni = DateTime.UtcNow
        };
        dbContext.ProjektSubsystemy.Add(entity);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "projekt_subsystemy", entity.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(entity));
    }

    public void DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
    {
        var entity = dbContext.ProjektSubsystemy.FirstOrDefault(x => x.Id == command.ProjektSubsystemId)
            ?? throw new InvalidOperationException("Projektový subsystém nebyl nalezen.");
        if (entity.DatumOdebrani.HasValue)
        {
            return;
        }

        var hasActiveRoles = dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Any(x => x.ProjektSubsystemId == entity.Id && !x.DatumOdebrani.HasValue);
        if (hasActiveRoles)
        {
            throw new InvalidOperationException("K přiřazení subsystému existují aktivní role. Nejdříve je deaktivujte.");
        }

        var old = JsonSerializer.Serialize(entity);
        entity.DatumOdebrani = DateTime.UtcNow;
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "projekt_subsystemy", entity.Id.ToString(CultureInfo.InvariantCulture), "deactivate", old, JsonSerializer.Serialize(entity));
    }

    public void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var projectSubsystem = dbContext.ProjektSubsystemy.AsNoTracking()
            .FirstOrDefault(x => x.Id == command.ProjektSubsystemId)
            ?? throw new InvalidOperationException("Projektový subsystém nebyl nalezen.");
        if (projectSubsystem.ProjektId != command.ProjektId)
        {
            throw new InvalidOperationException("Projektový subsystém nepatří do zvoleného projektu.");
        }

        if (projectSubsystem.DatumOdebrani.HasValue)
        {
            throw new InvalidOperationException("Do neaktivního projektového subsystému nelze přiřadit roli.");
        }

        var personId = command.OsobaId.Value;
        EnsurePersonExists(personId);
        if (PersonHasActiveHostProjectRole(command.ProjektId, personId))
        {
            throw new InvalidOperationException("Osoba s aktivní rolí Host nesmí dostat roli v subsystému.");
        }

        var role = dbContext.CiselnikRoliSubsystemu.FirstOrDefault(x => x.Kod == command.RoleKod || x.Nazev == command.RoleKod)
            ?? throw new InvalidOperationException($"Role subsystému '{command.RoleKod}' nebyla nalezena.");

        var exists = dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Any(x => x.ProjektSubsystemId == command.ProjektSubsystemId
                && x.OsobaId == personId
                && x.RoleSubsystemuId == role.Id
                && !x.DatumOdebrani.HasValue);
        if (exists)
        {
            throw new InvalidOperationException("Vybraná subsystemová role je už osobě aktivně přiřazena.");
        }

        var leadRoleId = dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        if (leadRoleId.HasValue && role.Id == leadRoleId.Value)
        {
            var leadExists = dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                .Any(x => x.ProjektSubsystemId == command.ProjektSubsystemId
                    && x.RoleSubsystemuId == leadRoleId.Value
                    && !x.DatumOdebrani.HasValue);
            if (leadExists)
            {
                throw new InvalidOperationException("Projektový subsystém už má aktivního vedoucího subsystému.");
            }
        }

        var entity = new ObsazeniSubsystemuProjektuEntity
        {
            ProjektSubsystemId = command.ProjektSubsystemId,
            OsobaId = personId,
            RoleSubsystemuId = role.Id,
            DatumPrirazeni = DateTime.UtcNow
        };
        dbContext.ObsazeniSubsystemuProjektu.Add(entity);
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_subsystemu_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(entity));
    }

    public void DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
    {
        var entity = dbContext.ObsazeniSubsystemuProjektu.FirstOrDefault(x => x.Id == command.ProjektSubsystemRoleId)
            ?? throw new InvalidOperationException("Subsystemová role nebyla nalezena.");
        if (entity.DatumOdebrani.HasValue)
        {
            return;
        }

        var old = JsonSerializer.Serialize(entity);
        entity.DatumOdebrani = DateTime.UtcNow;
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_subsystemu_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "deactivate", old, JsonSerializer.Serialize(entity));
    }

    private int ResolveSubsystemId(string value)
        => dbContext.Subsystemy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Subsystém '{value}' neexistuje.");

    private void EnsurePersonExists(int osobaId)
    {
        var exists = dbContext.Osoby.AsNoTracking().Any(x => x.Id == osobaId);
        if (!exists)
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }
    }

    private bool PersonHasActiveHostProjectRole(int projectId, int osobaId)
    {
        var hostRoleIds = dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == ProjectRoleCodes.Host)
            .Select(x => x.Id)
            .ToHashSet();

        return dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId
                && x.OsobaId == osobaId
                && !x.DatumOdebrani.HasValue)
            .Any(x => hostRoleIds.Contains(x.RoleId));
    }

    private bool PersonHasActiveSubsystemRole(int projectId, int osobaId)
    {
        return dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Join(
                dbContext.ProjektSubsystemy.AsNoTracking(),
                role => role.ProjektSubsystemId,
                projectSubsystem => projectSubsystem.Id,
                (role, projectSubsystem) => new { role, projectSubsystem })
            .Any(x => x.projectSubsystem.ProjektId == projectId
                && x.role.OsobaId == osobaId
                && !x.role.DatumOdebrani.HasValue
                && !x.projectSubsystem.DatumOdebrani.HasValue);
    }

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
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
    }
}
