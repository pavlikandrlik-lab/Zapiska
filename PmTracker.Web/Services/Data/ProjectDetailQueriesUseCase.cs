using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public sealed class ProjectDetailQueriesUseCase(PmTrackerDbContext dbContext) : IProjectDetailQueriesUseCase
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public ProjektDetailViewModel BuildProjektDetail(int id, IProjectDetailComposition composition)
    {
        var project = dbContext.Projekty.AsNoTracking().FirstOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException($"Projekt {id} nebyl nalezen.");

        var statusById = dbContext.CiselnikStavuProjektu.AsNoTracking().ToDictionary(x => x.Id);
        var projectRoleOptions = dbContext.CiselnikRoliProjektu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var subsystemRoleOptions = dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var activeProjectSubsystems = composition.BuildActiveProjectSubsystems(id);
        var subsystemFilterOptions = activeProjectSubsystems
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        var categoryFilterOptions = dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var taskStateFilterOptions = dbContext.CiselnikStavuUkolu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var taskTypeFilterOptions = dbContext.CiselnikTypuUkolu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var meetingStatusOptions = dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
        var meetingStatusFilterOptions = dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(CultureInfo.InvariantCulture),
                Label = x.Nazev
            })
            .ToList();

        var records = composition.BuildRecordCardsForProject(id);
        var harmonogramUkoly = composition.BuildProjectScheduleRows(records);
        var meetings = composition.BuildJednaniList(id);
        var aktivniRole = composition.BuildUnifiedActiveProjectRoleRows(id);
        var historieRoli = composition.BuildUnifiedProjectRoleHistoryRows(id);
        var dostupneOsoby = composition.BuildProjectMemberCandidates();
        var dostupneProjektoveSubsystemy = composition.BuildProjectSubsystemOptions(id);
        var dostupneSubsystemy = dbContext.Subsystemy.AsNoTracking()
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
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
            OtevrenaJednani = composition.BuildOpenMeetingOptions(meetings),
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
