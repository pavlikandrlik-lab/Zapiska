using System.Globalization;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    public async Task AssignProjectRoleAsync(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var personId = command.OsobaId.Value;
        var role = await dbContext.CiselnikRoliProjektu.FirstOrDefaultAsync(x => x.Kod == command.RoleKod || x.Nazev == command.RoleKod, ct)
            ?? throw new InvalidOperationException($"Role projektu '{command.RoleKod}' nebyla nalezena.");
        await EnsurePersonExistsAsync(personId, ct);

        var activeRolesForPerson = await dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == command.ProjektId && x.OsobaId == personId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var activeRoleIdsForPerson = activeRolesForPerson.Select(x => x.RoleId).ToHashSet();
        var hostRoleId = await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == ProjectRoleCodes.Host)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        var ownerRoleId = await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == ProjectRoleCodes.ProjectOwner)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

        if (activeRolesForPerson.Any(x => x.RoleId == role.Id))
        {
            throw new InvalidOperationException("Vybraná role je už osobě v projektu aktivně přiřazena.");
        }

        if (hostRoleId.HasValue && role.Id == hostRoleId.Value && activeRolesForPerson.Count > 0)
        {
            throw new InvalidOperationException("Role Host nesmí koexistovat s jinou aktivní projektovou rolí stejné osoby.");
        }

        if (hostRoleId.HasValue && role.Id == hostRoleId.Value && await PersonHasActiveSubsystemRoleAsync(command.ProjektId, personId, ct))
        {
            throw new InvalidOperationException("Osoba s aktivní rolí v subsystému nesmí mít projektovou roli Host.");
        }

        if (hostRoleId.HasValue && activeRoleIdsForPerson.Contains(hostRoleId.Value))
        {
            throw new InvalidOperationException("Osoba s aktivní rolí Host nesmí dostat další projektovou roli.");
        }

        if (ownerRoleId.HasValue && role.Id == ownerRoleId.Value)
        {
            var ownerExists = await dbContext.ObsazeniProjektu.AsNoTracking()
                .AnyAsync(x => x.ProjektId == command.ProjektId && x.RoleId == ownerRoleId.Value && !x.DatumOdebrani.HasValue, ct);
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
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(entity), ct);
    }

    public async Task DeactivateProjectRoleAsync(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var entity = await dbContext.ObsazeniProjektu.FirstOrDefaultAsync(x => x.Id == command.ProjektRoleId, ct)
            ?? throw new InvalidOperationException("Projektová role nebyla nalezena.");
        if (entity.DatumOdebrani.HasValue)
        {
            return;
        }

        var old = JsonSerializer.Serialize(entity);
        entity.DatumOdebrani = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "deactivate", old, JsonSerializer.Serialize(entity), ct);
    }

    public async Task AssignProjectSubsystemAsync(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var subsystemId = await ResolveSubsystemIdAsync(command.SubsystemKod, ct);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var exists = await dbContext.ProjektSubsystemy
            .AnyAsync(x => x.ProjektId == command.ProjektId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue, ct);
        if (exists)
        {
            throw new InvalidOperationException("Subsystém je už projektu aktivně přiřazen.");
        }

        var entity = new ProjektSubsystemEntity
        {
            ProjektId = command.ProjektId,
            SubsystemId = subsystemId,
            Poradi = await ResolveNextProjectSubsystemOrderAsync(command.ProjektId, ct),
            DatumPrirazeni = DateTime.UtcNow
        };
        dbContext.ProjektSubsystemy.Add(entity);
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "projekt_subsystemy", entity.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(entity), ct);
    }

    public async Task ReorderProjectSubsystemAsync(ReorderProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var direction = NormalizeProjectSubsystemReorderDirection(command.Direction);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var activeSubsystems = await dbContext.ProjektSubsystemy
            .Where(x => x.ProjektId == command.ProjektId && !x.DatumOdebrani.HasValue)
            .OrderBy(x => x.Poradi)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var currentIndex = activeSubsystems.FindIndex(x => x.Id == command.ProjektSubsystemId);
        if (currentIndex < 0)
        {
            throw new InvalidOperationException("Projektový subsystém nebyl nalezen.");
        }

        var targetIndex = string.Equals(direction, ProjectSubsystemReorderDirections.Up, StringComparison.Ordinal)
            ? currentIndex - 1
            : currentIndex + 1;
        if (targetIndex < 0 || targetIndex >= activeSubsystems.Count)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var current = activeSubsystems[currentIndex];
        var target = activeSubsystems[targetIndex];
        var currentOld = JsonSerializer.Serialize(current);
        var targetOld = JsonSerializer.Serialize(target);
        var currentOrder = current.Poradi;
        var targetOrder = target.Poradi;
        var temporaryOrder = activeSubsystems.Max(x => x.Poradi) + 1;

        current.Poradi = temporaryOrder;
        await dbContext.SaveChangesAsync(ct);

        target.Poradi = currentOrder;
        current.Poradi = targetOrder;
        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await WriteAuditAsync(currentUser.OsobaId, "projekt_subsystemy", current.Id.ToString(CultureInfo.InvariantCulture), "reorder", currentOld, JsonSerializer.Serialize(current), ct);
        await WriteAuditAsync(currentUser.OsobaId, "projekt_subsystemy", target.Id.ToString(CultureInfo.InvariantCulture), "reorder", targetOld, JsonSerializer.Serialize(target), ct);
    }

    public async Task DeactivateProjectSubsystemAsync(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var entity = await dbContext.ProjektSubsystemy.FirstOrDefaultAsync(x => x.Id == command.ProjektSubsystemId, ct)
            ?? throw new InvalidOperationException("Projektový subsystém nebyl nalezen.");
        if (entity.DatumOdebrani.HasValue)
        {
            return;
        }

        var hasActiveRoles = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .AnyAsync(x => x.ProjektSubsystemId == entity.Id && !x.DatumOdebrani.HasValue, ct);
        if (hasActiveRoles)
        {
            throw new InvalidOperationException("K přiřazení subsystému existují aktivní role. Nejdříve je deaktivujte.");
        }

        var old = JsonSerializer.Serialize(entity);
        entity.DatumOdebrani = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "projekt_subsystemy", entity.Id.ToString(CultureInfo.InvariantCulture), "deactivate", old, JsonSerializer.Serialize(entity), ct);
    }

    public async Task AssignProjectSubsystemRoleAsync(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var projectSubsystem = await dbContext.ProjektSubsystemy.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.ProjektSubsystemId, ct)
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
        await EnsurePersonExistsAsync(personId, ct);
        if (await PersonHasActiveHostProjectRoleAsync(command.ProjektId, personId, ct))
        {
            throw new InvalidOperationException("Osoba s aktivní rolí Host nesmí dostat roli v subsystému.");
        }

        var role = await dbContext.CiselnikRoliSubsystemu.FirstOrDefaultAsync(x => x.Kod == command.RoleKod || x.Nazev == command.RoleKod, ct)
            ?? throw new InvalidOperationException($"Role subsystému '{command.RoleKod}' nebyla nalezena.");

        var exists = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .AnyAsync(x => x.ProjektSubsystemId == command.ProjektSubsystemId
                && x.OsobaId == personId
                && x.RoleSubsystemuId == role.Id
                && !x.DatumOdebrani.HasValue,
                ct);
        if (exists)
        {
            throw new InvalidOperationException("Vybraná subsystemová role je už osobě aktivně přiřazena.");
        }

        var leadRoleId = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (leadRoleId.HasValue && role.Id == leadRoleId.Value)
        {
            var leadExists = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                .AnyAsync(x => x.ProjektSubsystemId == command.ProjektSubsystemId
                    && x.RoleSubsystemuId == leadRoleId.Value
                    && !x.DatumOdebrani.HasValue,
                    ct);
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
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_subsystemu_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(entity), ct);
    }

    public async Task DeactivateProjectSubsystemRoleAsync(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var entity = await dbContext.ObsazeniSubsystemuProjektu.FirstOrDefaultAsync(x => x.Id == command.ProjektSubsystemRoleId, ct)
            ?? throw new InvalidOperationException("Subsystemová role nebyla nalezena.");
        if (entity.DatumOdebrani.HasValue)
        {
            return;
        }

        var old = JsonSerializer.Serialize(entity);
        entity.DatumOdebrani = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_subsystemu_projektu", entity.Id.ToString(CultureInfo.InvariantCulture), "deactivate", old, JsonSerializer.Serialize(entity), ct);
    }

    private async Task<int> ResolveSubsystemIdAsync(string value, CancellationToken ct)
        => await dbContext.Subsystemy
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Subsystém '{value}' neexistuje.");

    private async Task EnsurePersonExistsAsync(int osobaId, CancellationToken ct)
    {
        var exists = await dbContext.Osoby.AsNoTracking().AnyAsync(x => x.Id == osobaId, ct);
        if (!exists)
        {
            throw new InvalidOperationException("Vybraná osoba neexistuje.");
        }
    }

    private async Task<bool> PersonHasActiveHostProjectRoleAsync(int projectId, int osobaId, CancellationToken ct)
    {
        var hostRoleIds = (await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == ProjectRoleCodes.Host)
            .Select(x => x.Id)
            .ToListAsync(ct))
            .ToHashSet();

        return await dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId
                && x.OsobaId == osobaId
                && !x.DatumOdebrani.HasValue)
            .AnyAsync(x => hostRoleIds.Contains(x.RoleId), ct);
    }

    private Task<bool> PersonHasActiveSubsystemRoleAsync(int projectId, int osobaId, CancellationToken ct)
    {
        return dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Join(
                dbContext.ProjektSubsystemy.AsNoTracking(),
                role => role.ProjektSubsystemId,
                projectSubsystem => projectSubsystem.Id,
                (role, projectSubsystem) => new { role, projectSubsystem })
            .AnyAsync(x => x.projectSubsystem.ProjektId == projectId
                && x.role.OsobaId == osobaId
                && !x.role.DatumOdebrani.HasValue
                && !x.projectSubsystem.DatumOdebrani.HasValue,
                ct);
    }

    private static string NormalizeProjectSubsystemReorderDirection(string? direction)
    {
        if (string.Equals(direction, ProjectSubsystemReorderDirections.Up, StringComparison.OrdinalIgnoreCase))
        {
            return ProjectSubsystemReorderDirections.Up;
        }

        if (string.Equals(direction, ProjectSubsystemReorderDirections.Down, StringComparison.OrdinalIgnoreCase))
        {
            return ProjectSubsystemReorderDirections.Down;
        }

        throw new InvalidOperationException("Neplatný směr přesunu subsystému.");
    }

}
