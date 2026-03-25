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

    public async Task<ProjektListItemViewModel?> GetProjectListItemAsync(int id, CancellationToken ct = default)
    {
        var project = await dbContext.Projekty.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Zkratka,
                Nazev = x.CelyNazev,
                x.StavId,
                x.PouzivatIdentJednani
            })
            .FirstOrDefaultAsync(ct);
        if (project is null)
        {
            return null;
        }

        var status = await dbContext.CiselnikStavuProjektu.AsNoTracking()
            .Where(x => x.Id == project.StavId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefaultAsync(ct);

        return new ProjektListItemViewModel
        {
            Id = project.Id,
            Zkratka = project.Zkratka,
            Nazev = project.Nazev,
            StavKod = status?.Kod,
            Stav = status?.Nazev ?? "-",
            PouzivatIdentJednani = project.PouzivatIdentJednani,
            CanEdit = true,
            CanDelete = true
        };
    }
}
