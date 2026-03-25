using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    public async Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, IProjectDetailComposition composition, CancellationToken ct = default)
    {
        _ = composition;

        var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"Projekt {id} nebyl nalezen.");
        var status = await dbContext.CiselnikStavuProjektu.AsNoTracking()
            .Where(x => x.Id == project.StavId)
            .Select(x => x.Nazev)
            .FirstOrDefaultAsync(ct);
        var recordsTab = await BuildProjectRecordsTabAsync(id, ct);

        return new ProjektDetailViewModel
        {
            Projekt = new ProjektHeaderViewModel
            {
                Id = project.Id,
                Nazev = project.CelyNazev,
                Zkratka = project.Zkratka,
                Stav = status ?? "-",
                PouzivatIdentJednani = project.PouzivatIdentJednani
            },
            ZaznamyTab = recordsTab
        };
    }
}
