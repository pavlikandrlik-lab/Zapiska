using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private async Task<List<ZaznamCardViewModel>> BuildRecordCardsForProjectAsync(int projectId, CancellationToken ct)
    {
        _ = commentAuthorizationPolicy;

        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);
        records = [.. OrderRecordsByVisibleNumber(records)];
        var recordIds = records.Select(record => record.Id).ToArray();

        var categories = (await dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var taskTypes = (await dbContext.CiselnikTypuUkolu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var taskStates = (await dbContext.CiselnikStavuUkolu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var subsystems = (await dbContext.Subsystemy.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var leadEquivalentOsobaIdsBySubsystem = await BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(projectId, ct);

        var ownerHistoryByRecord = (await dbContext.ZaznamHistorieVlastnik.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderBy(x => x.DatumZmeny)
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var termHistoryByRecord = (await dbContext.ZaznamHistorieTerminu.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderBy(x => x.DatumZmeny)
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var subsystemHistoryByRecord = (await dbContext.ZaznamHistorieSubsystem.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderBy(x => x.DatumZmeny)
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var typeHistoryByRecord = (await dbContext.ZaznamHistorieZmenTypu.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderBy(x => x.DatumZmeny)
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var extTypeById = (await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var vyzvaById = (await dbContext.CiselnikVyzvy.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var externalByRecord = (await dbContext.ZaznamExterniOdkazy.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderBy(x => x.Id)
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var collaborationByRecord = (await dbContext.ZaznamSpoluprace.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var comments = await dbContext.Vyjadreni.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        var personIds = records
            .Select(record => record.VlastnikId)
            .Concat(ownerHistoryByRecord.Values.SelectMany(history => history.Select(item => item.PuvodniVlastnik)))
            .Concat(comments.Select(comment => comment.AutorOsobaId))
            .Concat(collaborationByRecord.Values.SelectMany(rows => rows.Select(item => item.OsobaId)))
            .Distinct()
            .ToArray();
        var people = await LoadPeopleByIdsAsync(personIds, ct);
        var organizations = await LoadOrganizationsByPeopleAsync(people.Values, ct);
        var orgUnits = await LoadOrgUnitsByPeopleAsync(people.Values, ct);

        var meetingIds = comments
            .Select(comment => comment.JednaniId)
            .Distinct()
            .ToArray();
        var meetings = meetingIds.Length == 0
            ? new Dictionary<int, JednaniEntity>()
            : await dbContext.Jednani.AsNoTracking()
                .Where(x => meetingIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var meetingStates = (await dbContext.CiselnikStavuJednani.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);

        var commentByRecord = comments
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return records.Select(record =>
        {
            IReadOnlyList<ZaznamHistorieVlastnikEntity> ownerHistory = ownerHistoryByRecord.TryGetValue(record.Id, out var ownerHistoryValues)
                ? ownerHistoryValues
                : Array.Empty<ZaznamHistorieVlastnikEntity>();
            IReadOnlyList<ZaznamHistorieTerminuEntity> termHistory = termHistoryByRecord.TryGetValue(record.Id, out var termHistoryValues)
                ? termHistoryValues
                : Array.Empty<ZaznamHistorieTerminuEntity>();
            IReadOnlyList<ZaznamHistorieSubsystemEntity> subsystemHistory = subsystemHistoryByRecord.TryGetValue(record.Id, out var subsystemHistoryValues)
                ? subsystemHistoryValues
                : Array.Empty<ZaznamHistorieSubsystemEntity>();
            IReadOnlyList<ZaznamHistorieZmenTypuEntity> taskTypeHistory = typeHistoryByRecord.TryGetValue(record.Id, out var taskTypeHistoryValues)
                ? taskTypeHistoryValues
                : Array.Empty<ZaznamHistorieZmenTypuEntity>();
            IReadOnlyList<VyjadreniEntity> commentsForRecord = commentByRecord.TryGetValue(record.Id, out var commentValues)
                ? commentValues
                : Array.Empty<VyjadreniEntity>();
            var category = categories.GetValueOrDefault(record.KategorieId);
            var isTask = IsTaskCategory(category?.Kod, category?.Nazev);

            var commentMeetingStateCodes = commentsForRecord
                .Select(comment =>
                {
                    if (!meetings.TryGetValue(comment.JednaniId, out var meeting))
                    {
                        return null;
                    }

                    return meeting.StavJednaniId.ToString(CultureInfo.InvariantCulture);
                })
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!.Trim())
                .Distinct(Ci)
                .ToList();

            var vmComments = commentsForRecord.Select(comment =>
                {
                    meetings.TryGetValue(comment.JednaniId, out var meeting);
                    var meetingState = meeting is null ? null : meetingStates.GetValueOrDefault(meeting.StavJednaniId);
                    var author = people.GetValueOrDefault(comment.AutorOsobaId) ?? people.GetValueOrDefault(record.VlastnikId);
                    return new VyjadreniViewModel
                    {
                        Id = comment.Id,
                        AutorOsobaId = comment.AutorOsobaId,
                        Autor = BuildInlinePersonLabelFromOsoba(author),
                        Datum = comment.DatumVyjadreni,
                        Text = comment.TextVyjadreni,
                        JednaniCislo = meeting?.CisloJednani,
                        JednaniDatum = meeting?.DatumPlanovane,
                        LzeUpravit = !IsMeetingReadOnly(meeting, meetingState)
                    };
                })
                .OrderBy(x => x.JednaniCislo ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .ToList();

            var hasPreparationComment = commentsForRecord.Any(comment =>
            {
                if (!meetings.TryGetValue(comment.JednaniId, out var meeting))
                {
                    return false;
                }

                return meetingStates.TryGetValue(meeting.StavJednaniId, out var state)
                    && state.Kod.Equals("DRAFT", StringComparison.OrdinalIgnoreCase);
            });

            IReadOnlyList<ZaznamExterniOdkazEntity> externalByRecordValues = externalByRecord.TryGetValue(record.Id, out var externalValues)
                ? externalValues
                : Array.Empty<ZaznamExterniOdkazEntity>();
            var externalLinks = externalByRecordValues
                .Select(link =>
                {
                    var ticketId = ExtractServiceDeskTicketId(link.Cislo);
                    return new ExterniOdkazViewModel
                    {
                        Typ = extTypeById.GetValueOrDefault(link.TypOdkazuId)?.Kod ?? "-",
                        TypNazev = extTypeById.GetValueOrDefault(link.TypOdkazuId)?.Nazev,
                        Cislo = link.Cislo,
                        PredpokladanaCena = link.PredpokladanaCena,
                        ServiceDeskTicketId = ticketId,
                        ServiceDeskUrl = BuildServiceDeskUrl(ticketId),
                        Vyzva = link.Vyzva.HasValue ? vyzvaById.GetValueOrDefault(link.Vyzva.Value)?.Kod : null,
                        DatumObjednani = link.DatumObjednani,
                        DatumPlanDodani = link.PlanDodani,
                        DatumDodani = link.DatumDodani,
                        DatumPrevzeti = link.DatumPrevzeti
                    };
                })
                .ToList();

            IReadOnlyList<ZaznamSpolupraceEntity> collaborationValues = collaborationByRecord.TryGetValue(record.Id, out var collaborationRows)
                ? collaborationRows
                : Array.Empty<ZaznamSpolupraceEntity>();
            var collaboration = collaborationValues
                .Select(x =>
                {
                    var person = people.GetValueOrDefault(x.OsobaId);
                    return new SpolupracovnikViewModel
                    {
                        OsobaId = x.OsobaId,
                        Osoba = BuildDisplayNameFromOsoba(person),
                        Email = person?.Email?.Trim(),
                        Organizace = person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                        OrganizacniCelek = person?.OrganizacniCelekId is int cel ? orgUnits.GetValueOrDefault(cel)?.Nazev : null
                    };
                })
                .ToList();

            var currentOwner = people.GetValueOrDefault(record.VlastnikId);
            var currentSubsystem = subsystems.GetValueOrDefault(record.SubsystemId);
            var currentState = record.StavUkoluId.HasValue ? taskStates.GetValueOrDefault(record.StavUkoluId.Value) : null;
            var currentTaskType = record.AktualniTypUkoluId.HasValue ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value) : null;

            return new ZaznamCardViewModel
            {
                Id = record.Id,
                HarmonogramSablonaVerze = record.HarmonogramSablonaVerze,
                CisloZaznamu = record.CisloZaznamu,
                CisloViditelne = ResolveVisibleRecordNumber(record),
                Nazev = record.Nazev,
                KategorieKod = category?.Kod ?? "-",
                KategorieNazev = category?.Nazev ?? "-",
                TypUkoluKod = currentTaskType?.Kod,
                TypUkolu = currentTaskType?.Nazev,
                StavKod = currentState?.Kod,
                Stav = currentState?.Nazev ?? "-",
                IsAktivniStav = record.StavUkoluId.HasValue
                    ? !(currentState?.IsFinal ?? false)
                    : true,
                JeUkol = isTask,
                VyjadreniJednaniStavyKody = commentMeetingStateCodes,
                Cil = record.Cil ?? string.Empty,
                Popis = record.Popis ?? string.Empty,
                HistorieVlastniku = ownerHistory.Select(x => BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(x.PuvodniVlastnik))).Distinct(Ci).ToList(),
                AktualniVlastnik = BuildInlinePersonLabelFromOsoba(currentOwner),
                HistorieTerminu = termHistory.Select(x => x.PuvodniDatum).Distinct().ToList(),
                AktualniTermin = record.DatumUkonceni,
                HistorieSubsystemu = subsystemHistory.Select(x => subsystems.GetValueOrDefault(x.PuvodniSubsystem)?.Nazev ?? "-").Distinct(Ci).ToList(),
                HistorieTypuUkolu = taskTypeHistory.Select(x => taskTypes.GetValueOrDefault(x.PuvodniTypId)?.Nazev ?? "-").Distinct(Ci).ToList(),
                AktualniSubsystemKod = string.IsNullOrWhiteSpace(currentSubsystem?.Kod) ? (currentSubsystem?.Nazev ?? string.Empty) : currentSubsystem.Kod,
                AktualniSubsystem = currentSubsystem?.Nazev ?? "-",
                AktualniSubsystemLeadEquivalentOsobaIds = leadEquivalentOsobaIdsBySubsystem.GetValueOrDefault(record.SubsystemId, []),
                ExterniOdkazy = externalLinks,
                Spoluprace = collaboration,
                Vyjadreni = vmComments,
                MaVyjadreniProPripravuJednani = hasPreparationComment,
                DatumZalozeni = record.DatumZalozeni,
                AktualniVlastnikId = record.VlastnikId
            };
        }).ToList();
    }

    private async Task<Dictionary<int, OsobaEntity>> LoadPeopleByIdsAsync(IEnumerable<int> osobaIds, CancellationToken ct)
    {
        var ids = osobaIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        return await dbContext.Osoby.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    private async Task<Dictionary<int, CiselnikOrganizaceEntity>> LoadOrganizationsByPeopleAsync(IEnumerable<OsobaEntity> people, CancellationToken ct)
    {
        var organizationIds = people
            .Select(person => person.OrganizaceId)
            .Distinct()
            .ToArray();
        if (organizationIds.Length == 0)
        {
            return [];
        }

        return await dbContext.CiselnikOrganizace.AsNoTracking()
            .Where(x => organizationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    private async Task<Dictionary<int, CiselnikOrganizacniCelekEntity>> LoadOrgUnitsByPeopleAsync(IEnumerable<OsobaEntity> people, CancellationToken ct)
    {
        var orgUnitIds = people
            .Where(person => person.OrganizacniCelekId.HasValue)
            .Select(person => person.OrganizacniCelekId!.Value)
            .Distinct()
            .ToArray();
        if (orgUnitIds.Length == 0)
        {
            return [];
        }

        return await dbContext.CiselnikOrganizacniCelky.AsNoTracking()
            .Where(x => orgUnitIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    private async Task<Dictionary<int, List<int>>> BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(int projectId, CancellationToken ct)
    {
        var leadRoleIds = (await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToListAsync(ct))
            .ToHashSet();
        if (leadRoleIds.Count == 0)
        {
            return [];
        }

        var activeProjectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && leadRoleIds.Contains(x.RoleSubsystemuId))
            .ToListAsync(ct);

        return assignments
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList());
    }

    private async Task<IReadOnlyList<int>> ResolveLeadEquivalentOsobaIdsAsync(int projectId, int subsystemId, CancellationToken ct)
        => (await BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(projectId, ct)).GetValueOrDefault(subsystemId, []);
}
