using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services;

public sealed partial class MeetingService
{
    public async Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default)
    {
        var projects = await dbContext.Projekty.AsNoTracking()
            .OrderBy(x => x.Zkratka)
            .Select(x => new { x.Id, x.CelyNazev })
            .ToListAsync(ct);

        var allMeetings = await dbContext.Jednani.AsNoTracking()
            .ToListAsync(ct);
        var statusRows = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .ToListAsync(ct);
        var lockedPersonIds = allMeetings
            .Where(x => x.UzamklOsobaId.HasValue)
            .Select(x => x.UzamklOsobaId!.Value)
            .Distinct()
            .ToList();
        var lockedPersons = lockedPersonIds.Count == 0
            ? new Dictionary<int, OsobaEntity>()
            : await dbContext.Osoby.AsNoTracking()
                .Where(x => lockedPersonIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

        var statusById = statusRows.ToDictionary(x => x.Id);
        var meetingsByProjectId = allMeetings
            .GroupBy(x => x.ProjektId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<JednaniListItemViewModel>)MapJednaniList(group, statusById, lockedPersons));

        return projects
            .Select(project => new JednaniProjektListItemViewModel
            {
                ProjektId = project.Id,
                ProjektNazev = project.CelyNazev,
                Jednani = meetingsByProjectId.GetValueOrDefault(project.Id, [])
            })
            .Where(x => x.Jednani.Count > 0)
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
