using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Handlers;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    /// <summary>
    /// Zpětná kompatibilita: deleguje na vertical-slice <see cref="SaveProjectHandler"/>.
    /// Nová volání by měla injectovat handler přímo; interface bude odstraněn v příštím PR.
    /// </summary>
    public Task<int> SaveProjectAsync(SaveProjectCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
        => new SaveProjectHandler(dbContext, auditWriteService, textNormalizer)
            .HandleAsync(new SaveProjectRequest(command), currentUser, ct);

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

}
