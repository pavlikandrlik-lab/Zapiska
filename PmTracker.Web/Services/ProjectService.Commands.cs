using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    public async Task<int> SaveProjectAsync(SaveProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var statusId = await ResolveProjectStatusIdAsync(command.Stav, ct);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(ct);
            if (command.Id.HasValue)
            {
                var existing = await dbContext.Projekty.FirstOrDefaultAsync(x => x.Id == command.Id.Value, ct)
                    ?? throw new InvalidOperationException($"Projekt {command.Id.Value} nebyl nalezen.");

                var old = ProjectAuditSnapshot.FromEntity(existing);
                existing.CelyNazev = command.Nazev.Trim();
                existing.Zkratka = command.Zkratka.Trim();
                existing.StavId = statusId;
                existing.PouzivatIdentJednani = command.PouzivatIdentJednani;
                existing.MistoPlneni = NormalizeOrNull(command.MistoPlneni);
                existing.CisloRamcoveSmlouvy = NormalizeOrNull(command.CisloRamcoveSmlouvy);
                await dbContext.SaveChangesAsync(ct);
                auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Update,
                    AuditEntityType.Project,
                    existing.Id.ToString(CultureInfo.InvariantCulture),
                    old,
                    ProjectAuditSnapshot.FromEntity(existing)));
                await dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return existing.Id;
            }

            var created = new ProjektEntity
            {
                CelyNazev = command.Nazev.Trim(),
                Zkratka = command.Zkratka.Trim(),
                StavId = statusId,
                PouzivatIdentJednani = command.PouzivatIdentJednani,
                MistoPlneni = NormalizeOrNull(command.MistoPlneni),
                CisloRamcoveSmlouvy = NormalizeOrNull(command.CisloRamcoveSmlouvy)
            };
            dbContext.Projekty.Add(created);
            await dbContext.SaveChangesAsync(ct);

            auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Create,
                AuditEntityType.Project,
                created.Id.ToString(CultureInfo.InvariantCulture),
                null,
                ProjectAuditSnapshot.FromEntity(created)));
            await dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return created.Id;
        });
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

        var old = ProjectAuditSnapshot.FromEntity(project);
        project.StavId = deletedStatusId;
        await dbContext.SaveChangesAsync(ct);
        auditWriteService.Add(currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.SoftDelete,
            AuditEntityType.Project,
            project.Id.ToString(CultureInfo.InvariantCulture),
            old,
            ProjectAuditSnapshot.FromEntity(project)));
        await dbContext.SaveChangesAsync(ct);
    }

    private static string? NormalizeOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
