using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    public async Task<int> SaveProjectAsync(SaveProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var statusId = await ResolveProjectStatusIdAsync(command.Stav, ct);
        if (command.Id.HasValue)
        {
            var existing = await dbContext.Projekty.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                ?? throw new InvalidOperationException($"Projekt {command.Id.Value} nebyl nalezen.");

            var old = JsonSerializer.Serialize(existing);
            existing.CelyNazev = command.Nazev.Trim();
            existing.Zkratka = command.Zkratka.Trim();
            existing.StavId = statusId;
            existing.PouzivatIdentJednani = command.PouzivatIdentJednani;
            await dbContext.SaveChangesAsync(ct);
            await WriteAuditAsync(currentUser.OsobaId, "projekty", existing.Id.ToString(CultureInfo.InvariantCulture), "update", old, JsonSerializer.Serialize(existing), ct);
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
        await dbContext.SaveChangesAsync(ct);

        await WriteAuditAsync(currentUser.OsobaId, "projekty", created.Id.ToString(CultureInfo.InvariantCulture), "create", null, JsonSerializer.Serialize(created), ct);
        return created.Id;
    }

    public async Task SoftDeleteProjectAsync(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var deletedStatusId = (await dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync(ct))
            .FirstOrDefault(x =>
                string.Equals(x.Kod, "DELETED", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Nazev) && x.Nazev.Contains("smaz", StringComparison.OrdinalIgnoreCase)))
            ?.Id
            ?? throw new InvalidOperationException("V číselníku stavu projektu chybí stav DELETED/Smazáno.");

        var project = await dbContext.Projekty.FirstOrDefaultAsync(x => x.Id == command.ProjektId, ct)
            ?? throw new InvalidOperationException($"Projekt {command.ProjektId} nebyl nalezen.");

        var old = JsonSerializer.Serialize(project);
        project.StavId = deletedStatusId;
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "projekty", project.Id.ToString(CultureInfo.InvariantCulture), "soft_delete", old, JsonSerializer.Serialize(project), ct);
    }

    private async Task<int> ResolveProjectStatusIdAsync(string value, CancellationToken ct)
    {
        var input = value?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new InvalidOperationException("Stav projektu není vyplněn.");
        }

        var direct = await dbContext.CiselnikStavuProjektu
            .Where(x => x.Kod == input || x.Nazev == input)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (direct.HasValue)
        {
            return direct.Value;
        }

        var normalized = textNormalizer.Normalize(input);
        var rows = await dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync(ct);
        var match = rows
            .FirstOrDefault(x =>
                textNormalizer.Normalize(x.Kod) == normalized ||
                textNormalizer.Normalize(x.Nazev) == normalized);

        if (match is not null)
        {
            return match.Id;
        }

        throw new InvalidOperationException($"Stav projektu '{value}' neexistuje.");
    }

}
