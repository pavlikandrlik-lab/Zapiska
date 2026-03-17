using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Data;

public sealed class ProjectCommandsUseCase(
    PmTrackerDbContext dbContext,
    ITextNormalizer textNormalizer) : IProjectCommandsUseCase
{
    public int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser)
    {
        var statusId = ResolveProjectStatusId(command.Stav);
        if (command.Id.HasValue)
        {
            var existing = dbContext.Projekty.FirstOrDefault(x => x.Id == command.Id.Value)
                ?? throw new InvalidOperationException($"Projekt {command.Id.Value} nebyl nalezen.");

            var old = JsonSerializer.Serialize(existing);
            existing.CelyNazev = command.Nazev.Trim();
            existing.Zkratka = command.Zkratka.Trim();
            existing.StavId = statusId;
            existing.PouzivatIdentJednani = command.PouzivatIdentJednani;
            dbContext.SaveChanges();
            WriteAudit(currentUser.OsobaId, "projekty", existing.Id.ToString(CultureInfo.InvariantCulture), "update", old, JsonSerializer.Serialize(existing));
            return existing.Id;
        }

        var created = new ProjektEntity
        {
            CelyNazev = command.Nazev.Trim(),
            Zkratka = command.Zkratka.Trim(),
            StavId = statusId,
            PouzivatIdentJednani = command.PouzivatIdentJednani
        };
        dbContext.Projekty.Add(created);
        dbContext.SaveChanges();

        WriteAudit(currentUser.OsobaId, "projekty", created.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(created));
        return created.Id;
    }

    public void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser)
    {
        var deletedStatusId = dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToList()
            .FirstOrDefault(x =>
                string.Equals(x.Kod, "DELETED", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Nazev) && x.Nazev.Contains("smaz", StringComparison.OrdinalIgnoreCase)))
            ?.Id
            ?? throw new InvalidOperationException("V číselníku stavu projektu chybí stav DELETED/Smazáno.");

        var project = dbContext.Projekty.FirstOrDefault(x => x.Id == command.ProjektId)
            ?? throw new InvalidOperationException($"Projekt {command.ProjektId} nebyl nalezen.");

        var old = JsonSerializer.Serialize(project);
        project.StavId = deletedStatusId;
        dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "projekty", project.Id.ToString(CultureInfo.InvariantCulture), "soft_delete", old, JsonSerializer.Serialize(project));
    }

    private int ResolveProjectStatusId(string value)
    {
        var input = value?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Stav projektu není vyplněn.");
        }

        var direct = dbContext.CiselnikStavuProjektu
            .Where(x => x.Kod == input || x.Nazev == input)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        if (direct.HasValue)
        {
            return direct.Value;
        }

        var normalized = textNormalizer.Normalize(input);
        var match = dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .AsEnumerable()
            .FirstOrDefault(x =>
                textNormalizer.Normalize(x.Kod) == normalized ||
                textNormalizer.Normalize(x.Nazev) == normalized);

        if (match is not null)
        {
            return match.Id;
        }

        throw new InvalidOperationException($"Stav projektu '{value}' neexistuje.");
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
