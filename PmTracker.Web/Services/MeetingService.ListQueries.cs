using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services;

public sealed partial class MeetingService
{
    public async Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default)
        => await BuildJednaniOverviewAsync(projectIds: null, ct);

    public async Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(IReadOnlyCollection<int>? projectIds, CancellationToken ct = default)
    {
        if (projectIds is { Count: 0 })
        {
            return [];
        }

        var meetingRowsQuery =
            from meeting in dbContext.Jednani.AsNoTracking()
            join project in dbContext.Projekty.AsNoTracking() on meeting.ProjektId equals project.Id
            join projectStatus in dbContext.CiselnikStavuProjektu.AsNoTracking() on project.StavId equals projectStatus.Id
            join status in dbContext.CiselnikStavuJednani.AsNoTracking() on meeting.StavJednaniId equals status.Id
            select new
            {
                ProjektId = project.Id,
                ProjektNazev = project.CelyNazev,
                ProjektZkratka = project.Zkratka,
                ProjektStavKod = projectStatus.Kod,
                ProjektStav = projectStatus.Nazev,
                ProjektMistoPlneni = project.MistoPlneni,
                ProjectSortKey = project.Zkratka,
                meeting.Id,
                meeting.CisloJednani,
                Datum = meeting.DatumPlanovane,
                meeting.CasZacatek,
                meeting.Misto,
                StavKod = status.Kod,
                Stav = status.Nazev,
                meeting.UzamklOsobaId
            };

        if (projectIds is { Count: > 0 })
        {
            meetingRowsQuery = meetingRowsQuery.Where(x => projectIds.Contains(x.ProjektId));
        }

        var rows = await meetingRowsQuery
            .OrderBy(x => x.ProjectSortKey)
            .ThenByDescending(x => x.CisloJednani)
            .ThenByDescending(x => x.Datum)
            .ThenByDescending(x => x.CasZacatek)
            .ToListAsync(ct);

        var lockedPersonIds = rows
            .Where(x => x.UzamklOsobaId.HasValue)
            .Select(x => x.UzamklOsobaId!.Value)
            .Distinct()
            .ToList();
        var lockedPersons = lockedPersonIds.Count == 0
            ? new Dictionary<int, OsobaEntity>()
            : await dbContext.Osoby.AsNoTracking()
                .Where(x => lockedPersonIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var currentYear = timeProvider.GetUtcNow().Year;

        return rows
            .GroupBy(x => new { x.ProjektId, x.ProjektNazev, x.ProjektZkratka, x.ProjektStavKod, x.ProjektStav, x.ProjektMistoPlneni })
            .Select(group =>
            {
                var meetings = group
                    .Select(x => new JednaniListItemViewModel
                    {
                        Id = x.Id,
                        CisloJednani = x.CisloJednani,
                        Datum = x.Datum,
                        CasZacatek = x.CasZacatek,
                        Misto = string.IsNullOrWhiteSpace(x.Misto) ? "-" : x.Misto,
                        StavKod = x.StavKod,
                        Stav = x.Stav,
                        UzamklOsoba = x.UzamklOsobaId.HasValue
                            ? BuildInlinePersonLabelFromOsoba(lockedPersons.GetValueOrDefault(x.UzamklOsobaId.Value))
                            : null
                    })
                    .ToList();

                return new JednaniProjektListItemViewModel
                {
                    ProjektId = group.Key.ProjektId,
                    ProjektNazev = group.Key.ProjektNazev,
                    ProjektZkratka = group.Key.ProjektZkratka,
                    ProjektStavKod = group.Key.ProjektStavKod,
                    ProjektStav = group.Key.ProjektStav,
                    MistoPlneni = group.Key.ProjektMistoPlneni,
                    PocetCelkem = meetings.Count,
                    PocetLetos = meetings.Count(m => m.Datum.Year == currentYear),
                    Jednani = meetings,
                    RocniSkupiny = MeetingYearGroupBuilder.BuildYearGroups(meetings)
                };
            })
            .OrderBy(x => x.ProjektNazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default)
    {
        var meetings = await dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .ToListAsync(ct);

        var statusRows = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .ToListAsync(ct);
        var lockedPersonIds = meetings
            .Where(x => x.UzamklOsobaId.HasValue)
            .Select(x => x.UzamklOsobaId!.Value)
            .Distinct()
            .ToList();
        var personsById = lockedPersonIds.Count == 0
            ? new Dictionary<int, OsobaEntity>()
            : await dbContext.Osoby.AsNoTracking()
                .Where(x => lockedPersonIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        return MapJednaniList(meetings, statusRows.ToDictionary(x => x.Id), personsById);
    }

    private List<JednaniListItemViewModel> MapJednaniList(
        IEnumerable<JednaniEntity> meetings,
        IReadOnlyDictionary<int, CiselnikStavuJednaniEntity> statusById,
        IReadOnlyDictionary<int, OsobaEntity> personsById)
    {
        return meetings
            .OrderByDescending(x => x.CisloJednani)
            .ThenByDescending(x => x.DatumPlanovane)
            .ThenByDescending(x => x.CasZacatek)
            .Select(x => new JednaniListItemViewModel
            {
                Id = x.Id,
                CisloJednani = x.CisloJednani,
                Datum = x.DatumPlanovane,
                CasZacatek = x.CasZacatek,
                Misto = string.IsNullOrWhiteSpace(x.Misto) ? "-" : x.Misto,
                StavKod = statusById.GetValueOrDefault(x.StavJednaniId)?.Kod,
                Stav = statusById.GetValueOrDefault(x.StavJednaniId)?.Nazev ?? "-",
                UzamklOsoba = x.UzamklOsobaId.HasValue ? BuildInlinePersonLabelFromOsoba(personsById.GetValueOrDefault(x.UzamklOsobaId.Value)) : null
            })
            .ToList();
    }

}
