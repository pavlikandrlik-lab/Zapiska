using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private const int RecordCommentsLoadStep = 5;

    private sealed record RecordSummaryRow(
        int Id,
        int ProjektId,
        int HarmonogramSablonaVerze,
        int CisloZaznamu,
        string? CisloViditelne,
        int CisloViditelneA,
        int CisloViditelneB,
        byte CisloViditelneTyp,
        string Nazev,
        int KategorieId,
        int? AktualniTypUkoluId,
        int? StavUkoluId,
        string? Cil,
        DateTime DatumZalozeni,
        DateTime? DatumUkonceni,
        int SubsystemId,
        int VlastnikId);

    public async Task<ProjektZaznamyTabViewModel> BuildProjectRecordsTabAsync(int id, CancellationToken ct = default)
    {
        var summaries = await BuildRecordCardSummariesForProjectAsync(id, ct);
        var activeProjectSubsystems = await BuildActiveProjectSubsystemsAsync(id, ct);
        var subsystemFilterOptions = activeProjectSubsystems
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();
        // Perf: projekce do LookupOptionViewModel z cached ciselnik dictionary (OrderBy in-memory).
        var categoryFilterOptions = (await lookupCache.GetCategoriesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToList();
        var taskStateFilterOptions = (await lookupCache.GetTaskStatesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToList();
        var taskTypeFilterOptions = (await lookupCache.GetTaskTypesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToList();
        var meetingStatusFilterOptions = (await lookupCache.GetMeetingStatesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Id.ToString(CultureInfo.InvariantCulture), Label = x.Nazev })
            .ToList();
        var ownerFilterOptions = summaries
            .Where(x => x.Summary.AktualniVlastnikId > 0)
            .GroupBy(x => x.Summary.AktualniVlastnikId)
            .Select(group => new LookupOptionViewModel
            {
                Value = group.Key.ToString(CultureInfo.InvariantCulture),
                Label = group.First().Summary.AktualniVlastnik
            })
            .OrderBy(x => x.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ProjektZaznamyTabViewModel
        {
            ProjektId = id,
            DleSubsystemu = true,
            SkupinyZaznamu = summaries
                .GroupBy(x => new
                {
                    x.Summary.AktualniSubsystem,
                    x.Summary.AktualniSubsystemKod,
                    x.Summary.AktualniSubsystemPoradi,
                    x.Summary.AktualniSubsystemHasProjectOrder
                })
                .OrderByDescending(x => x.Key.AktualniSubsystemHasProjectOrder)
                .ThenBy(x => x.Key.AktualniSubsystemPoradi)
                .ThenBy(x => x.Key.AktualniSubsystem, StringComparer.CurrentCultureIgnoreCase)
                .Select(group => new ProjektZaznamGroupViewModel
                {
                    Nazev = group.Key.AktualniSubsystem,
                    Kod = group.Key.AktualniSubsystemKod,
                    Poradi = group.Key.AktualniSubsystemPoradi,
                    HasProjectOrder = group.Key.AktualniSubsystemHasProjectOrder,
                    Zaznamy = group.ToList()
                })
                .ToList(),
            Zaznamy = summaries,
            Filtry = new ProjektFiltryViewModel
            {
                Subsystemy = summaries.Select(x => x.Summary.AktualniSubsystem).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                SubsystemyMoznosti = subsystemFilterOptions,
                Kategorie = summaries.Select(x => x.Summary.KategorieNazev).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                KategorieMoznosti = categoryFilterOptions,
                StavyUkolu = summaries.Select(x => x.Summary.Stav).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                StavyUkoluMoznosti = taskStateFilterOptions,
                TypyUkolu = summaries.Where(x => !string.IsNullOrWhiteSpace(x.Summary.TypUkolu)).Select(x => x.Summary.TypUkolu!).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                TypyUkoluMoznosti = taskTypeFilterOptions,
                Vlastnici = summaries.Select(x => x.Summary.AktualniVlastnik).Distinct(Ci).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList(),
                VlastniciMoznosti = ownerFilterOptions,
                StavyJednaniVyjadreni = meetingStatusFilterOptions
            }
        };
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<string>>> BuildRecordMeetingCommentStatesAsync(int projectId, CancellationToken ct = default)
    {
        var recordIds = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Select(x => x.Id)
            .ToArrayAsync(ct);

        return await BuildRecordMeetingCommentStateMapAsync(recordIds, ct);
    }

    public async Task<ProjektHarmonogramTabViewModel> BuildProjectScheduleTabAsync(int id, CancellationToken ct = default)
    {
        var scheduleRecords = await BuildScheduleRecordCardsForProjectAsync(id, ct);
        var harmonogramUkoly = await BuildProjectScheduleRowsAsync(scheduleRecords, ct);
        var activeProjectSubsystems = await BuildActiveProjectSubsystemsAsync(id, ct);
        var subsystemOptions = activeProjectSubsystems
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : x.Kod,
                Label = string.IsNullOrWhiteSpace(x.Kod) ? x.Nazev : $"{x.Kod} - {x.Nazev}"
            })
            .ToList();

        return new ProjektHarmonogramTabViewModel
        {
            ProjektId = id,
            SubsystemyMoznosti = subsystemOptions,
            HarmonogramUkoly = harmonogramUkoly
        };
    }

    public async Task<ProjektJednaniTabViewModel> BuildProjectMeetingsTabAsync(int id, CancellationToken ct = default)
    {
        var meetings = await BuildJednaniListAsync(id, ct);
        var meetingStatusOptions = (await lookupCache.GetMeetingStatesAsync(ct))
            .Values.OrderBy(x => x.Id)
            .Select(x => new LookupOptionViewModel { Value = x.Kod, Label = x.Nazev })
            .ToList();

        return new ProjektJednaniTabViewModel
        {
            ProjektId = id,
            Jednani = meetings,
            RocniSkupiny = MeetingYearGroupBuilder.BuildYearGroups(meetings),
            StavyJednani = meetingStatusOptions
        };
    }

    public async Task<ProjektTymTabViewModel> BuildProjectTeamTabAsync(int id, CancellationToken ct = default)
    {
        return new ProjektTymTabViewModel
        {
            ProjektId = id,
            AktivniRole = await BuildUnifiedActiveProjectRoleRowsAsync(id, ct),
            HistorieRoli = await BuildUnifiedProjectRoleHistoryRowsAsync(id, ct),
            AktivniSubsystemyProjektu = await BuildProjectTeamSubsystemRowsAsync(id, ct),
            DostupneOsobyProRole = [],
            DostupneProjektoveSubsystemy = [],
            RoleProjektu = [],
            RoleSubsystemu = [],
            DostupneSubsystemy = []
        };
    }

    public async Task<ProjektZaznamCardShellViewModel?> BuildRecordCardShellAsync(int projectId, int recordId, CancellationToken ct = default)
    {
        var summary = await BuildRecordCardSummaryAsync(projectId, recordId, ct);
        if (summary is null)
        {
            return null;
        }

        return new ProjektZaznamCardShellViewModel
        {
            Summary = summary,
            DetailLoaded = false,
            CommentsLoaded = false
        };
    }

    public async Task<ZaznamCardDetailViewModel?> BuildRecordCardDetailAsync(int projectId, int recordId, CancellationToken ct = default)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.Id == recordId)
            .Select(x => new
            {
                x.Id,
                x.Popis,
                x.VlastnikId,
                x.DatumUkonceni,
                x.SubsystemId,
                x.AktualniTypUkoluId
            })
            .FirstOrDefaultAsync(ct);
        if (record is null)
        {
            return null;
        }

        var ownerHistory = await dbContext.ZaznamHistorieVlastnik.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .OrderBy(x => x.DatumZmeny)
            .ToListAsync(ct);
        var termHistory = await dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .OrderBy(x => x.DatumZmeny)
            .ToListAsync(ct);
        var subsystemHistory = await dbContext.ZaznamHistorieSubsystem.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .OrderBy(x => x.DatumZmeny)
            .ToListAsync(ct);
        var taskTypeHistory = await dbContext.ZaznamHistorieZmenTypu.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .OrderBy(x => x.DatumZmeny)
            .ToListAsync(ct);
        var externalLinks = await dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var collaborationRows = await dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .ToListAsync(ct);

        var personIds = ownerHistory.Select(x => x.PuvodniVlastnik)
            .Append(record.VlastnikId)
            .Concat(collaborationRows.Select(x => x.OsobaId))
            .Distinct()
            .ToArray();
        var people = await LoadPeopleByIdsAsync(personIds, ct);
        var organizations = await LoadOrganizationsByPeopleAsync(people.Values, ct);
        var orgUnits = await LoadOrgUnitsByPeopleAsync(people.Values, ct);
        var subsystemIds = subsystemHistory
            .SelectMany(x => new[] { x.PuvodniSubsystem, x.NovySubsystem })
            .Append(record.SubsystemId)
            .Distinct()
            .ToArray();
        var taskTypeIds = taskTypeHistory
            .SelectMany(x => new[] { x.PuvodniTypId, x.NovyTypId })
            .Append(record.AktualniTypUkoluId ?? 0)
            .Where(x => x > 0)
            .Distinct()
            .ToArray();
        var externalTypeIds = externalLinks
            .Select(x => x.TypOdkazuId)
            .Distinct()
            .ToArray();
        var vyzvaIds = externalLinks
            .Where(x => x.VyzvaId.HasValue)
            .Select(x => x.VyzvaId!.Value)
            .Distinct()
            .ToArray();

        // Perf: cache-backed lookups — filtered by IDs in-memory (O(1) dict lookups).
        // Pokud už cache je naplněný (Projekty/Detail nejdřív zavolá BuildRecordCardsForProjectAsync),
        // tyto řádky jsou free; jinak stáhnou celou tabulku, která se pak reusuje i dalšími call-sity.
        var allSubsystems = await lookupCache.GetSubsystemsAsync(ct);
        var allTaskTypes = await lookupCache.GetTaskTypesAsync(ct);
        var allExtTypes = await lookupCache.GetExternalLinkTypesAsync(ct);
        var allVyzvy = await lookupCache.GetVyzvyAsync(ct);

        var subsystems = subsystemIds.Length == 0
            ? new Dictionary<int, SubsystemEntity>()
            : subsystemIds.Where(id => allSubsystems.ContainsKey(id)).ToDictionary(id => id, id => allSubsystems[id]);
        var taskTypes = taskTypeIds.Length == 0
            ? new Dictionary<int, CiselnikTypuUkoluEntity>()
            : taskTypeIds.Where(id => allTaskTypes.ContainsKey(id)).ToDictionary(id => id, id => allTaskTypes[id]);
        var extTypeById = externalTypeIds.Length == 0
            ? new Dictionary<int, CiselnikTypuExternichOdkazuEntity>()
            : externalTypeIds.Where(id => allExtTypes.ContainsKey(id)).ToDictionary(id => id, id => allExtTypes[id]);
        var vyzvaById = vyzvaIds.Length == 0
            ? new Dictionary<int, VyzvaEntity>()
            : vyzvaIds.Where(id => allVyzvy.ContainsKey(id)).ToDictionary(id => id, id => allVyzvy[id]);

        return new ZaznamCardDetailViewModel
        {
            Popis = record.Popis ?? string.Empty,
            HistorieVlastniku = ownerHistory
                .Select(x => BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(x.PuvodniVlastnik)))
                .Distinct(Ci)
                .ToList(),
            AktualniVlastnik = BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(record.VlastnikId)),
            HistorieTerminu = termHistory.Select(x => x.PuvodniDatum).Distinct().ToList(),
            AktualniTermin = record.DatumUkonceni,
            HistorieSubsystemu = subsystemHistory
                .Select(x => subsystems.GetValueOrDefault(x.PuvodniSubsystem)?.Nazev ?? "-")
                .Distinct(Ci)
                .ToList(),
            AktualniSubsystem = subsystems.GetValueOrDefault(record.SubsystemId)?.Nazev ?? "-",
            TypUkolu = record.AktualniTypUkoluId.HasValue ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value)?.Nazev : null,
            HistorieTypuUkolu = taskTypeHistory
                .Select(x => taskTypes.GetValueOrDefault(x.PuvodniTypId)?.Nazev ?? "-")
                .Distinct(Ci)
                .ToList(),
            ExterniOdkazy = externalLinks.Select(link =>
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
                    Vyzva = link.VyzvaId.HasValue ? vyzvaById.GetValueOrDefault(link.VyzvaId.Value)?.Kod : null,
                    DatumObjednani = link.DatumObjednani,
                    DatumPlanDodani = link.PlanDodani,
                    DatumDodani = link.DatumDodani,
                    DatumPrevzeti = link.DatumPrevzeti
                };
            }).ToList(),
            Spoluprace = collaborationRows.Select(row =>
            {
                var person = people.GetValueOrDefault(row.OsobaId);
                return new SpolupracovnikViewModel
                {
                    OsobaId = row.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(person),
                    Email = person?.Email?.Trim(),
                    Organizace = person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                    OrganizacniCelek = person?.OrganizacniCelekId is int cel ? orgUnits.GetValueOrDefault(cel)?.Nazev : null
                };
            }).ToList()
        };
    }

    public Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, CancellationToken ct = default)
        => BuildRecordCommentsPanelAsync(projectId, recordId, limit: null, loadAll: false, ct);

    public async Task<ZaznamCommentsPanelViewModel?> BuildRecordCommentsPanelAsync(int projectId, int recordId, int? limit, bool loadAll, CancellationToken ct = default)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.Id == recordId)
            .Select(x => new
            {
                x.Id,
                x.VlastnikId
            })
            .FirstOrDefaultAsync(ct);
        if (record is null)
        {
            return null;
        }

        var totalCount = await dbContext.Vyjadreni.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .CountAsync(ct);
        var effectiveLimit = ResolveRecordCommentsLimit(limit, loadAll, totalCount);
        var comments = await dbContext.Vyjadreni.AsNoTracking()
            .Where(x => x.ZaznamId == recordId)
            .OrderByDescending(x => x.DatumVyjadreni)
            .ThenByDescending(x => x.Id)
            .Take(effectiveLimit)
            .ToListAsync(ct);
        var personIds = comments.Select(x => x.AutorOsobaId)
            .Append(record.VlastnikId)
            .Distinct()
            .ToArray();
        var people = await LoadPeopleByIdsAsync(personIds, ct);
        var meetingIds = comments.Select(x => x.JednaniId).Distinct().ToArray();
        var meetings = meetingIds.Length == 0
            ? new Dictionary<int, JednaniEntity>()
            : await dbContext.Jednani.AsNoTracking()
                .Where(x => meetingIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
        var meetingStateIds = meetings.Values.Select(x => x.StavJednaniId).Distinct().ToArray();
        var allMeetingStates = await lookupCache.GetMeetingStatesAsync(ct);
        var meetingStates = meetingStateIds.Length == 0
            ? new Dictionary<int, CiselnikStavuJednaniEntity>()
            : meetingStateIds.Where(allMeetingStates.ContainsKey).ToDictionary(id => id, id => allMeetingStates[id]);
        var meetingOptions = await BuildOpenMeetingOptionsForProjectAsync(projectId, ct);
        var loadedCount = comments.Count;
        var isFullyLoaded = loadedCount >= totalCount;

        return new ZaznamCommentsPanelViewModel
        {
            ProjektId = projectId,
            ZaznamId = recordId,
            LoadedCount = loadedCount,
            TotalCount = totalCount,
            LoadStep = RecordCommentsLoadStep,
            CanLoadMore = !isFullyLoaded,
            CanLoadAll = !isFullyLoaded,
            IsFullyLoaded = isFullyLoaded,
            OtevrenaJednani = meetingOptions,
            Vyjadreni = comments
                .Select(comment =>
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
                        LzeUpravit = !IsMeetingReadOnly(meeting, meetingState),
                        CanEditOwnAsSubsystemLeader = !IsMeetingReadOnly(meeting, meetingState)
                            && string.Equals(meetingState?.Kod, "DRAFT", StringComparison.OrdinalIgnoreCase)
                    };
                })
                .ToList()
        };
    }

    private static int ResolveRecordCommentsLimit(int? limit, bool loadAll, int totalCount)
    {
        if (totalCount <= 0)
        {
            return 0;
        }

        if (loadAll)
        {
            return totalCount;
        }

        var normalizedLimit = limit.GetValueOrDefault(RecordCommentsLoadStep);
        if (normalizedLimit <= 0)
        {
            normalizedLimit = RecordCommentsLoadStep;
        }

        return Math.Min(totalCount, normalizedLimit);
    }

    private async Task<List<JednaniOptionViewModel>> BuildOpenMeetingOptionsForProjectAsync(int projectId, CancellationToken ct)
    {
        var rows = await (
                from meeting in dbContext.Jednani.AsNoTracking()
                join state in dbContext.CiselnikStavuJednani.AsNoTracking() on meeting.StavJednaniId equals state.Id
                where meeting.ProjektId == projectId
                    && !meeting.UzamklOsobaId.HasValue
                    && state.Kod != "CLOSED"
                    && !EF.Functions.Like(state.Nazev, "%uzav%")
                orderby meeting.CisloJednani descending, meeting.DatumPlanovane descending
                select new
                {
                    meeting.Id,
                    meeting.CisloJednani,
                    meeting.DatumPlanovane,
                    StavKod = state.Kod,
                    Stav = state.Nazev
                })
            .ToListAsync(ct);

        return rows
            .Select(row => new JednaniOptionViewModel
            {
                Id = row.Id,
                Label = $"Jednání č. {row.CisloJednani} ({row.DatumPlanovane:dd.MM.yyyy})",
                Datum = row.DatumPlanovane.Date,
                StavKod = row.StavKod
            })
            .ToList();
    }

    private async Task<List<ProjektZaznamCardShellViewModel>> BuildRecordCardSummariesForProjectAsync(int projectId, CancellationToken ct)
        => (await BuildRecordCardSummariesCoreAsync(projectId, null, ct))
            .Select(summary => new ProjektZaznamCardShellViewModel
            {
                Summary = summary,
                DetailLoaded = false,
                CommentsLoaded = false
            })
            .ToList();

    private async Task<ZaznamCardSummaryViewModel?> BuildRecordCardSummaryAsync(int projectId, int recordId, CancellationToken ct)
        => (await BuildRecordCardSummariesCoreAsync(projectId, [recordId], ct)).FirstOrDefault();

    private async Task<List<ZaznamCardSummaryViewModel>> BuildRecordCardSummariesCoreAsync(int projectId, IReadOnlyCollection<int>? recordIdsFilter, CancellationToken ct)
    {
        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && (recordIdsFilter == null || recordIdsFilter.Contains(x.Id)))
            .Select(x => new RecordSummaryRow(
                x.Id,
                x.ProjektId,
                x.HarmonogramSablonaVerze,
                x.CisloZaznamu,
                x.CisloViditelne,
                x.CisloViditelneA,
                x.CisloViditelneB,
                x.CisloViditelneTyp,
                x.Nazev,
                x.KategorieId,
                x.AktualniTypUkoluId,
                x.StavUkoluId,
                x.Cil,
                x.DatumZalozeni,
                x.DatumUkonceni,
                x.SubsystemId,
                x.VlastnikId))
            .ToListAsync(ct);
        if (records.Count == 0)
        {
            return [];
        }

        records = records
            .OrderBy(ResolveVisibleNumberPartA)
            .ThenBy(ResolveVisibleNumberPartB)
            .ThenBy(x => x.CisloZaznamu)
            .ToList();
        var ownerIds = records.Select(x => x.VlastnikId).Distinct().ToArray();
        var categoryIds = records.Select(x => x.KategorieId).Distinct().ToArray();
        var taskTypeIds = records.Where(x => x.AktualniTypUkoluId.HasValue).Select(x => x.AktualniTypUkoluId!.Value).Distinct().ToArray();
        var taskStateIds = records.Where(x => x.StavUkoluId.HasValue).Select(x => x.StavUkoluId!.Value).Distinct().ToArray();
        var subsystemIds = records.Select(x => x.SubsystemId).Distinct().ToArray();
        // Perf: cache-backed lookups — viz LookupTableCache + ProjectQueryHelpers.
        var allCategories = await lookupCache.GetCategoriesAsync(ct);
        var allTaskTypes = await lookupCache.GetTaskTypesAsync(ct);
        var allTaskStates = await lookupCache.GetTaskStatesAsync(ct);
        var allSubsystems = await lookupCache.GetSubsystemsAsync(ct);

        var categories = categoryIds.Length == 0
            ? new Dictionary<int, CiselnikKategoriiZaznamuEntity>()
            : categoryIds.Where(allCategories.ContainsKey).ToDictionary(id => id, id => allCategories[id]);
        var taskTypes = taskTypeIds.Length == 0
            ? new Dictionary<int, CiselnikTypuUkoluEntity>()
            : taskTypeIds.Where(allTaskTypes.ContainsKey).ToDictionary(id => id, id => allTaskTypes[id]);
        var taskStates = taskStateIds.Length == 0
            ? new Dictionary<int, CiselnikStavuUkoluEntity>()
            : taskStateIds.Where(allTaskStates.ContainsKey).ToDictionary(id => id, id => allTaskStates[id]);
        var subsystems = subsystemIds.Length == 0
            ? new Dictionary<int, SubsystemEntity>()
            : subsystemIds.Where(allSubsystems.ContainsKey).ToDictionary(id => id, id => allSubsystems[id]);
        var subsystemOrderById = await BuildActiveProjectSubsystemOrderBySubsystemIdAsync(projectId, subsystemIds, ct);
        var people = await LoadPeopleByIdsAsync(ownerIds, ct);
        var leadEquivalentOsobaIdsBySubsystem = await BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(projectId, ct);

        return records.Select(record =>
        {
            var category = categories.GetValueOrDefault(record.KategorieId);
            var currentTaskType = record.AktualniTypUkoluId.HasValue ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value) : null;
            var currentState = record.StavUkoluId.HasValue ? taskStates.GetValueOrDefault(record.StavUkoluId.Value) : null;
            var currentSubsystem = subsystems.GetValueOrDefault(record.SubsystemId);
            var hasProjectOrder = subsystemOrderById.TryGetValue(record.SubsystemId, out var subsystemOrder);
            return new ZaznamCardSummaryViewModel
            {
                Id = record.Id,
                ProjektId = record.ProjektId,
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
                JeUkol = IsTaskCategory(category?.Kod, category?.Nazev),
                VyjadreniJednaniStavyKody = Array.Empty<string>(),
                Cil = record.Cil ?? string.Empty,
                AktualniVlastnik = BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(record.VlastnikId)),
                AktualniTermin = record.DatumUkonceni,
                AktualniSubsystemKod = string.IsNullOrWhiteSpace(currentSubsystem?.Kod) ? (currentSubsystem?.Nazev ?? string.Empty) : currentSubsystem.Kod,
                AktualniSubsystem = currentSubsystem?.Nazev ?? "-",
                AktualniSubsystemPoradi = hasProjectOrder ? subsystemOrder : 0,
                AktualniSubsystemHasProjectOrder = hasProjectOrder,
                AktualniSubsystemLeadEquivalentOsobaIds = leadEquivalentOsobaIdsBySubsystem.GetValueOrDefault(record.SubsystemId, []),
                DatumZalozeni = record.DatumZalozeni,
                AktualniVlastnikId = record.VlastnikId
            };
        }).ToList();
    }

    private async Task<Dictionary<int, IReadOnlyList<string>>> BuildRecordMeetingCommentStateMapAsync(
        IReadOnlyCollection<int> recordIds,
        CancellationToken ct)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        return (await (
                from comment in dbContext.Vyjadreni.AsNoTracking()
                join meeting in dbContext.Jednani.AsNoTracking() on comment.JednaniId equals meeting.Id
                where recordIds.Contains(comment.ZaznamId)
                select new
                {
                    comment.ZaznamId,
                    StavJednaniId = meeting.StavJednaniId
                })
            .ToListAsync(ct))
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(x => x.StavJednaniId.ToString(CultureInfo.InvariantCulture))
                    .Distinct(Ci)
                    .ToList());
    }

    private async Task<List<ZaznamCardViewModel>> BuildScheduleRecordCardsForProjectAsync(int projectId, CancellationToken ct)
    {
        var records = await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Select(x => new RecordSummaryRow(
                x.Id,
                x.ProjektId,
                x.HarmonogramSablonaVerze,
                x.CisloZaznamu,
                x.CisloViditelne,
                x.CisloViditelneA,
                x.CisloViditelneB,
                x.CisloViditelneTyp,
                x.Nazev,
                x.KategorieId,
                x.AktualniTypUkoluId,
                x.StavUkoluId,
                x.Cil,
                x.DatumZalozeni,
                x.DatumUkonceni,
                x.SubsystemId,
                x.VlastnikId))
            .ToListAsync(ct);
        if (records.Count == 0)
        {
            return [];
        }

        records = records
            .OrderBy(ResolveVisibleNumberPartA)
            .ThenBy(ResolveVisibleNumberPartB)
            .ThenBy(x => x.CisloZaznamu)
            .ToList();
        var ownerIds = records.Select(x => x.VlastnikId).Distinct().ToArray();
        var categoryIds = records.Select(x => x.KategorieId).Distinct().ToArray();
        var taskTypeIds = records.Where(x => x.AktualniTypUkoluId.HasValue).Select(x => x.AktualniTypUkoluId!.Value).Distinct().ToArray();
        var taskStateIds = records.Where(x => x.StavUkoluId.HasValue).Select(x => x.StavUkoluId!.Value).Distinct().ToArray();
        var subsystemIds = records.Select(x => x.SubsystemId).Distinct().ToArray();
        // Perf: cache-backed lookups.
        var allCategoriesB = await lookupCache.GetCategoriesAsync(ct);
        var allTaskTypesB = await lookupCache.GetTaskTypesAsync(ct);
        var allTaskStatesB = await lookupCache.GetTaskStatesAsync(ct);
        var allSubsystemsB = await lookupCache.GetSubsystemsAsync(ct);

        var categories = categoryIds.Length == 0
            ? new Dictionary<int, CiselnikKategoriiZaznamuEntity>()
            : categoryIds.Where(allCategoriesB.ContainsKey).ToDictionary(id => id, id => allCategoriesB[id]);
        var taskTypes = taskTypeIds.Length == 0
            ? new Dictionary<int, CiselnikTypuUkoluEntity>()
            : taskTypeIds.Where(allTaskTypesB.ContainsKey).ToDictionary(id => id, id => allTaskTypesB[id]);
        var taskStates = taskStateIds.Length == 0
            ? new Dictionary<int, CiselnikStavuUkoluEntity>()
            : taskStateIds.Where(allTaskStatesB.ContainsKey).ToDictionary(id => id, id => allTaskStatesB[id]);
        var subsystems = subsystemIds.Length == 0
            ? new Dictionary<int, SubsystemEntity>()
            : subsystemIds.Where(allSubsystemsB.ContainsKey).ToDictionary(id => id, id => allSubsystemsB[id]);
        var subsystemOrderById = await BuildActiveProjectSubsystemOrderBySubsystemIdAsync(projectId, subsystemIds, ct);
        var people = await LoadPeopleByIdsAsync(ownerIds, ct);

        return records.Select(record =>
        {
            var category = categories.GetValueOrDefault(record.KategorieId);
            var currentTaskType = record.AktualniTypUkoluId.HasValue ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value) : null;
            var currentState = record.StavUkoluId.HasValue ? taskStates.GetValueOrDefault(record.StavUkoluId.Value) : null;
            var currentSubsystem = subsystems.GetValueOrDefault(record.SubsystemId);
            var hasProjectOrder = subsystemOrderById.TryGetValue(record.SubsystemId, out var subsystemOrder);

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
                JeUkol = IsTaskCategory(category?.Kod, category?.Nazev),
                VyjadreniJednaniStavyKody = Array.Empty<string>(),
                Cil = record.Cil ?? string.Empty,
                Popis = string.Empty,
                HistorieVlastniku = Array.Empty<string>(),
                AktualniVlastnik = BuildInlinePersonLabelFromOsoba(people.GetValueOrDefault(record.VlastnikId)),
                HistorieTerminu = Array.Empty<DateTime>(),
                AktualniTermin = record.DatumUkonceni,
                HistorieSubsystemu = Array.Empty<string>(),
                HistorieTypuUkolu = Array.Empty<string>(),
                AktualniSubsystemKod = string.IsNullOrWhiteSpace(currentSubsystem?.Kod) ? (currentSubsystem?.Nazev ?? string.Empty) : currentSubsystem.Kod,
                AktualniSubsystem = currentSubsystem?.Nazev ?? "-",
                AktualniSubsystemPoradi = hasProjectOrder ? subsystemOrder : 0,
                AktualniSubsystemHasProjectOrder = hasProjectOrder,
                AktualniSubsystemLeadEquivalentOsobaIds = Array.Empty<int>(),
                ExterniOdkazy = Array.Empty<ExterniOdkazViewModel>(),
                Spoluprace = Array.Empty<SpolupracovnikViewModel>(),
                Vyjadreni = Array.Empty<VyjadreniViewModel>(),
                DatumZalozeni = record.DatumZalozeni,
                AktualniVlastnikId = record.VlastnikId
            };
        }).ToList();
    }

    private static string ResolveVisibleRecordNumber(RecordSummaryRow record)
    {
        if (!string.IsNullOrWhiteSpace(record.CisloViditelne))
        {
            return record.CisloViditelne.Trim();
        }

        return record.CisloZaznamu.ToString(CultureInfo.InvariantCulture);
    }

    private static int ResolveVisibleNumberPartA(RecordSummaryRow record)
    {
        if (record.CisloViditelneA > 0)
        {
            return record.CisloViditelneA;
        }

        return Math.Max(0, record.CisloZaznamu);
    }

    private static int ResolveVisibleNumberPartB(RecordSummaryRow record)
    {
        if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
        {
            return Math.Max(1, record.CisloViditelneB);
        }

        return 0;
    }
}
