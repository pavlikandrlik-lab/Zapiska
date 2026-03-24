using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    public async Task<IReadOnlyList<ProjektListItemViewModel>> BuildProjektyListAsync(CancellationToken ct = default)
    {
        var projects = await dbContext.Projekty.AsNoTracking()
            .ToListAsync(ct);
        var statusRows = await dbContext.CiselnikStavuProjektu.AsNoTracking()
            .ToListAsync(ct);
        var statuses = statusRows.ToDictionary(x => x.Id);

        return projects
            .OrderBy(x => x.Zkratka, StringComparer.CurrentCultureIgnoreCase)
            .Select(project => new ProjektListItemViewModel
            {
                Id = project.Id,
                Zkratka = project.Zkratka,
                Nazev = project.CelyNazev,
                StavKod = statuses.GetValueOrDefault(project.StavId)?.Kod,
                Stav = statuses.GetValueOrDefault(project.StavId)?.Nazev ?? "-",
                PouzivatIdentJednani = project.PouzivatIdentJednani,
                CanEdit = true,
                CanDelete = true
            })
            .ToList();
    }
}
