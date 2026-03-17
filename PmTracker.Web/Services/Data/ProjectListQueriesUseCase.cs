using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public sealed class ProjectListQueriesUseCase(PmTrackerDbContext dbContext) : IProjectListQueriesUseCase
{
    public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList()
    {
        var projects = dbContext.Projekty.AsNoTracking().ToList();
        var statuses = dbContext.CiselnikStavuProjektu.AsNoTracking().ToDictionary(x => x.Id);

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
