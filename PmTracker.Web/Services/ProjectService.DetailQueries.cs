using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    public async Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, IProjectDetailComposition composition, CancellationToken ct = default)
    {
        var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException($"Projekt {id} nebyl nalezen.");

        var statusRows = await dbContext.CiselnikStavuProjektu.AsNoTracking().ToListAsync(ct);
        var statusById = statusRows.ToDictionary(x => x.Id);
        var projectRoleOptions = await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
        var subsystemRoleOptions = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
        var activeProjectSubsystems = await composition.BuildActiveProjectSubsystemsAsync(id, ct);
        var subsystemFilterOptions = activeProjectSubsystems
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        var categoryFilterOptions = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
        var taskStateFilterOptions = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
        var taskTypeFilterOptions = await dbContext.CiselnikTypuUkolu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
        var meetingStatusOptions = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
        var meetingStatusFilterOptions = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(CultureInfo.InvariantCulture),
                Label = x.Nazev
            })
            .ToListAsync(ct);

        var records = await composition.BuildRecordCardsForProjectAsync(id, ct);
        var harmonogramUkoly = await composition.BuildProjectScheduleRowsAsync(records, ct);
        var meetings = await composition.BuildJednaniListAsync(id, ct);
        var aktivniRole = await composition.BuildUnifiedActiveProjectRoleRowsAsync(id, ct);
        var historieRoli = await composition.BuildUnifiedProjectRoleHistoryRowsAsync(id, ct);
        var dostupneOsoby = await composition.BuildProjectMemberCandidatesAsync(ct);
        var dostupneProjektoveSubsystemy = await composition.BuildProjectSubsystemOptionsAsync(id, ct);
        var dostupneSubsystemy = await dbContext.Subsystemy.AsNoTracking()
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToListAsync(ct);
        var ownerFilterOptions = records
            .Where(x => x.AktualniVlastnikId > 0)
            .GroupBy(x => x.AktualniVlastnikId)
            .Select(group => new LookupOptionViewModel
            {
                Value = group.Key.ToString(CultureInfo.InvariantCulture),
                Label = group.First().AktualniVlastnik
            })
            .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ProjektDetailViewModel
        {
            Projekt = new ProjektHeaderViewModel
            {
                Id = project.Id,
                Nazev = project.CelyNazev,
                Zkratka = project.Zkratka,
                Stav = statusById.GetValueOrDefault(project.StavId)?.Nazev ?? "-",
                PouzivatIdentJednani = project.PouzivatIdentJednani
            },
            DleSubsystemu = true,
            SkupinySubsystemu = records
                .GroupBy(x => x.AktualniSubsystem)
                .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(group => new SubsystemGroupViewModel
                {
                    Nazev = group.Key,
                    Zaznamy = group.ToList()
                })
                .ToList(),
            Zaznamy = records,
            Jednani = meetings,
            AktivniRole = aktivniRole,
            HistorieRoli = historieRoli,
            AktivniSubsystemyProjektu = activeProjectSubsystems,
            DostupneOsobyProRole = dostupneOsoby,
            DostupneProjektoveSubsystemy = dostupneProjektoveSubsystemy,
            HarmonogramUkoly = harmonogramUkoly,
            RoleProjektu = projectRoleOptions,
            RoleSubsystemu = subsystemRoleOptions,
            DostupneSubsystemy = dostupneSubsystemy,
            OtevrenaJednani = await composition.BuildOpenMeetingOptionsAsync(meetings, ct),
            StavyJednani = meetingStatusOptions,
            Filtry = new ProjektFiltryViewModel
            {
                Subsystemy = records.Select(x => x.AktualniSubsystem).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                SubsystemyMoznosti = subsystemFilterOptions,
                Kategorie = records.Select(x => x.KategorieNazev).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                KategorieMoznosti = categoryFilterOptions,
                StavyUkolu = records.Select(x => x.Stav).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                StavyUkoluMoznosti = taskStateFilterOptions,
                TypyUkolu = records.Where(x => !string.IsNullOrWhiteSpace(x.TypUkolu)).Select(x => x.TypUkolu!).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                TypyUkoluMoznosti = taskTypeFilterOptions,
                Vlastnici = records.Select(x => x.AktualniVlastnik).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                VlastniciMoznosti = ownerFilterOptions,
                StavyJednaniVyjadreni = meetingStatusFilterOptions
            }
        };
    }
}
