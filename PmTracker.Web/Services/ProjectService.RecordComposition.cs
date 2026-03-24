using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services;

public sealed partial class ProjectService
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;
    private const byte RecordDisplayNumberTypeIncrement = 0;
    private const byte RecordDisplayNumberTypeMeeting = 1;
    private static readonly IReadOnlyDictionary<int, int> EmptyIntMap = new Dictionary<int, int>();

    private sealed record ActiveProjectMembershipRow(
        int OsobaId,
        string Osoba,
        string? Email,
        string? Organizace,
        string? OrganizacniCelek,
        bool HasProjectRole,
        bool HasNonHostProjectRole,
        bool HasHostRole,
        bool HasSubsystemRole,
        IReadOnlyList<string> AktivniRole);

    public Task<bool> ProjektExistsAsync(int id, CancellationToken ct = default)
        => dbContext.Projekty.AsNoTracking().AnyAsync(x => x.Id == id, ct);

    public Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, CancellationToken ct = default)
        => BuildProjektDetailAsync(id, (IProjectDetailComposition)this, ct);

    public async Task<IReadOnlyList<LookupOptionViewModel>> BuildProjectStatusOptionsAsync(CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        return await dbContext.CiselnikStavuProjektu.AsNoTracking()
            .OrderBy(x => x.Nazev)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToListAsync(ct);
    }

    async Task<IReadOnlyList<ProjectSubsystemViewModel>> IProjectDetailComposition.BuildActiveProjectSubsystemsAsync(int projectId, CancellationToken ct)
        => await BuildActiveProjectSubsystemsAsync(projectId, ct);

    async Task<IReadOnlyList<ZaznamCardViewModel>> IProjectDetailComposition.BuildRecordCardsForProjectAsync(int projectId, CancellationToken ct)
        => await BuildRecordCardsForProjectAsync(projectId, ct);

    async Task<IReadOnlyList<ProjektHarmonogramUkolViewModel>> IProjectDetailComposition.BuildProjectScheduleRowsAsync(IReadOnlyList<ZaznamCardViewModel> records, CancellationToken ct)
        => await BuildProjectScheduleRowsAsync(records, ct);

    Task<IReadOnlyList<JednaniListItemViewModel>> IProjectDetailComposition.BuildJednaniListAsync(int projectId, CancellationToken ct)
        => BuildJednaniListAsync(projectId, ct);

    async Task<IReadOnlyList<ProjectRoleGridRowViewModel>> IProjectDetailComposition.BuildUnifiedActiveProjectRoleRowsAsync(int projectId, CancellationToken ct)
        => await BuildUnifiedActiveProjectRoleRowsAsync(projectId, ct);

    async Task<IReadOnlyList<ProjectRoleHistoryGridRowViewModel>> IProjectDetailComposition.BuildUnifiedProjectRoleHistoryRowsAsync(int projectId, CancellationToken ct)
        => await BuildUnifiedProjectRoleHistoryRowsAsync(projectId, ct);

    async Task<IReadOnlyList<ProjectMemberCandidateViewModel>> IProjectDetailComposition.BuildProjectMemberCandidatesAsync(CancellationToken ct)
        => await BuildProjectMemberCandidatesAsync(ct);

    async Task<IReadOnlyList<ProjectSubsystemOptionViewModel>> IProjectDetailComposition.BuildProjectSubsystemOptionsAsync(int projectId, CancellationToken ct)
        => await BuildProjectSubsystemOptionsAsync(projectId, ct);

    async Task<IReadOnlyList<JednaniOptionViewModel>> IProjectDetailComposition.BuildOpenMeetingOptionsAsync(IReadOnlyList<JednaniListItemViewModel> meetings, CancellationToken ct)
        => await BuildOpenMeetingOptionsAsync(meetings, ct);

    Task<ZaznamEditViewModel> IRecordEditorQueriesComposition.BuildZaznamEditForEntityAsync(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber,
        bool? projectUsesMeetingIdentifier,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions,
        CancellationToken ct)
        => BuildZaznamEditForEntityAsync(record, isCreate, forceMeetingIdForNumber, projectUsesMeetingIdentifier, openMeetingOptions, ct);

    async Task<IReadOnlyList<ProjectSubsystemViewModel>> IRecordEditorQueriesComposition.BuildActiveProjectSubsystemsAsync(int projectId, CancellationToken ct)
        => await BuildActiveProjectSubsystemsAsync(projectId, ct);

    Task<IReadOnlyList<JednaniListItemViewModel>> IRecordEditorQueriesComposition.BuildJednaniListAsync(int projectId, CancellationToken ct)
        => BuildJednaniListAsync(projectId, ct);

    async Task<IReadOnlyList<JednaniOptionViewModel>> IRecordEditorQueriesComposition.BuildOpenMeetingOptionsAsync(IReadOnlyList<JednaniListItemViewModel> meetings, CancellationToken ct)
        => await BuildOpenMeetingOptionsAsync(meetings, ct);

    int? IRecordEditorQueriesComposition.ResolveSelectedMeetingIdForNumber(
        IReadOnlyList<JednaniOptionViewModel> openMeetingOptions,
        int? contextMeetingId)
        => ResolveSelectedMeetingIdForNumber(openMeetingOptions, contextMeetingId);

    async Task<IReadOnlyDictionary<int, int>> IRecordEditorQueriesComposition.BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(int projectId, CancellationToken ct)
        => await BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(projectId, ct);

    Task<int> IRecordEditorQueriesComposition.EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct)
        => harmonogramService.EnsurePersistedActiveHarmonogramSchemaVersionAsync(ct);

    Task<int> IRecordEditorQueriesComposition.GetNextCisloZaznamuAsync(int projectId, CancellationToken ct)
        => GetNextCisloZaznamuAsync(projectId, ct);

    Task<int> IRecordWriteCommandsComposition.GetNextCisloZaznamuTransactionalAsync(int projektId, CancellationToken ct)
        => GetNextCisloZaznamuTransactionalAsync(projektId, ct);

    Task<int> IRecordWriteCommandsComposition.AllocateMeetingOrderTransactionalAsync(int projektId, int cisloJednani, CancellationToken ct)
        => AllocateMeetingOrderTransactionalAsync(projektId, cisloJednani, ct);

    async Task<IReadOnlyList<SpolupracovnikOptionViewModel>> IRecordWriteCommandsComposition.BuildRecordOwnerCandidatesAsync(int projectId, int? selectedOwnerId, CancellationToken ct)
        => await BuildRecordOwnerCandidatesAsync(projectId, selectedOwnerId, ct);

    Task<int> IRecordWriteCommandsComposition.EnsurePersistedActiveHarmonogramSchemaVersionAsync(CancellationToken ct)
        => harmonogramService.EnsurePersistedActiveHarmonogramSchemaVersionAsync(ct);

    async Task<IReadOnlyList<RecordScheduleTypeDefinition>> IRecordWriteCommandsComposition.ResolveScheduleTypeDefinitionsForRecordAsync(ProjektovyZaznamEntity record, CancellationToken ct)
        => harmonogramService.BuildRecordScheduleTypeDefinitions(await harmonogramService.GetSchemaForRecordAsync(record, ct));

    async Task<IReadOnlyList<RecordScheduleTypeDefinition>> IRecordWriteCommandsComposition.ResolveScheduleTypeDefinitionsForSchemaVersionAsync(int schemaVersion, CancellationToken ct)
        => harmonogramService.BuildRecordScheduleTypeDefinitions(await harmonogramService.GetSchemaForRecordAsync(schemaVersion, ct));

    Task<IReadOnlyList<int>> IRecordWriteCommandsComposition.ResolveLeadEquivalentOsobaIdsAsync(int projectId, int subsystemId, CancellationToken ct)
        => ResolveLeadEquivalentOsobaIdsAsync(projectId, subsystemId, ct);

    public Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default)
        => meetingService.BuildJednaniListAsync(projektId, ct);

    public async Task SaveTeamMemberAsync(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var osobaId = command.OsobaId.Value;
        var roleId = await ResolveProjectRoleIdAsync(command.Role, ct)
            ?? throw new InvalidOperationException($"Role '{command.Role}' nebyla nalezena.");

        var existing = await dbContext.ObsazeniProjektu
            .FirstOrDefaultAsync(x => x.ProjektId == command.ProjektId && x.OsobaId == osobaId, ct);
        if (existing is null)
        {
            dbContext.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
            {
                ProjektId = command.ProjektId,
                OsobaId = osobaId,
                RoleId = roleId
            });
        }
        else
        {
            existing.RoleId = roleId;
        }

        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{osobaId}", "upsert", null, JsonSerializer.Serialize(command), ct);
    }

    public async Task RemoveTeamMemberAsync(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default)
    {
        var rows = await dbContext.ObsazeniProjektu
            .Where(x => x.ProjektId == command.ProjektId && x.OsobaId == command.OsobaId)
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            return;
        }

        dbContext.ObsazeniProjektu.RemoveRange(rows);
        await dbContext.SaveChangesAsync(ct);
        await WriteAuditAsync(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{command.OsobaId}", "delete", null, null, ct);
    }

    public async Task<int> GetNextCisloZaznamuAsync(int projektId, CancellationToken ct = default)
    {
        var existingValues = await dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .Select(x => x.CisloZaznamu)
            .ToListAsync(ct);

        return RecordNumberAllocator.FindLowestAvailablePositive(existingValues);
    }

    private static string ResolveVisibleRecordNumber(ProjektovyZaznamEntity record)
    {
        if (!string.IsNullOrWhiteSpace(record.CisloViditelne))
        {
            return record.CisloViditelne.Trim();
        }

        return record.CisloZaznamu.ToString(CultureInfo.InvariantCulture);
    }

    private static int ResolveVisibleNumberPartA(ProjektovyZaznamEntity record)
    {
        if (record.CisloViditelneA > 0)
        {
            return record.CisloViditelneA;
        }

        return Math.Max(0, record.CisloZaznamu);
    }

    private static int ResolveVisibleNumberPartB(ProjektovyZaznamEntity record)
    {
        if (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting)
        {
            return Math.Max(1, record.CisloViditelneB);
        }

        return 0;
    }

    private static IEnumerable<ProjektovyZaznamEntity> OrderRecordsByVisibleNumber(IEnumerable<ProjektovyZaznamEntity> rows)
        => rows
            .OrderBy(ResolveVisibleNumberPartA)
            .ThenBy(ResolveVisibleNumberPartB)
            .ThenBy(x => x.CisloZaznamu);

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

    private async Task<List<ProjektHarmonogramUkolViewModel>> BuildProjectScheduleRowsAsync(IReadOnlyList<ZaznamCardViewModel> records, CancellationToken ct)
    {
        var taskRecords = records
            .Where(x => x.JeUkol)
            .ToList();

        if (taskRecords.Count == 0)
        {
            return [];
        }

        var taskRecordIds = taskRecords.Select(x => x.Id).ToList();
        var harmonogramRows = await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => taskRecordIds.Contains(x.ZaznamId))
            .ToListAsync(ct);
        var harmonogramByRecord = harmonogramRows
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<int, int>)group.ToDictionary(item => item.TypId, item => item.HodnotaInt));
        var schemaCache = new Dictionary<int, HarmonogramSchemaDefinition>();
        var ownerIds = taskRecords.Select(x => x.AktualniVlastnikId).Distinct().ToList();
        var ownerRows = await dbContext.Osoby.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.OrganizacniCelekId
            })
            .ToListAsync(ct);
        var ownerById = ownerRows.ToDictionary(x => x.Id);
        var ownerOrgUnitIds = ownerById.Values
            .Where(x => x.OrganizacniCelekId.HasValue)
            .Select(x => x.OrganizacniCelekId!.Value)
            .Distinct()
            .ToList();
        var ownerOrgCodeRows = await dbContext.CiselnikOrganizacniCelky.AsNoTracking()
            .Where(x => ownerOrgUnitIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Kod })
            .ToListAsync(ct);
        var ownerOrgCodes = ownerOrgCodeRows.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Kod) ? null : x.Kod.Trim());
        var schemaVersions = taskRecords
            .Select(x => x.HarmonogramSablonaVerze > 0 ? x.HarmonogramSablonaVerze : 0)
            .Distinct()
            .ToList();
        foreach (var schemaVersion in schemaVersions)
        {
            schemaCache[schemaVersion] = await harmonogramService.GetSchemaForRecordAsync(schemaVersion, ct);
        }

        var today = timeProvider.GetLocalNow().LocalDateTime.Date;

        return taskRecords
            .Select(record =>
            {
                var deadline = (record.AktualniTermin ?? record.DatumZalozeni).Date;
                var schemaVersion = record.HarmonogramSablonaVerze > 0 ? record.HarmonogramSablonaVerze : 0;
                var schema = schemaCache[schemaVersion];

                var harmonogramHodnoty = harmonogramByRecord.TryGetValue(record.Id, out var harmonogramValues)
                    ? harmonogramValues
                    : EmptyIntMap;
                var vypocet = harmonogramService.BuildHarmonogramVypocetPublic(record.DatumZalozeni, schema.Kroky, harmonogramHodnoty);
                var souhrn = harmonogramService.BuildHarmonogramSouhrn(vypocet, deadline);
                var owner = ownerById.GetValueOrDefault(record.AktualniVlastnikId);
                var ownerOrgCode = owner?.OrganizacniCelekId is int orgId ? ownerOrgCodes.GetValueOrDefault(orgId) : null;
                var ownerDisplay = owner is null
                    ? record.AktualniVlastnik
                    : BuildDisplayName(owner.Titul, owner.Jmeno, owner.Prijmeni, owner.Id);
                var hasVisualDuration = souhrn.CelkoveTrvaniDni > 0;

                if (!hasVisualDuration)
                {
                    return null;
                }

                var kroky = vypocet
                    .Select(krok => new ProjektHarmonogramKrokViewModel
                    {
                        KrokIndex = krok.KrokIndex,
                        Nazev = krok.Nazev,
                        TrvaniDni = krok.TrvaniDni,
                        OdchylkaDni = krok.ZpozdeniDni,
                        BarvaHex = krok.BarvaHex,
                        PlanStart = krok.PlanStartDatum.Date,
                        PlanEnd = krok.BaselineDatum.Date,
                        RealStart = krok.RealStartDatum.Date,
                        RealEnd = krok.PosunuteDatum.Date
                    })
                    .ToList();

                var compactAxisStart = record.DatumZalozeni.Date;
                var compactAxisEnd = new[] { deadline, souhrn.SkutecneDokonceni.Date, compactAxisStart }.Max();
                var compactAxisDays = Math.Max(1, (compactAxisEnd - compactAxisStart).Days);
                var breakdownDates = kroky
                    .SelectMany(krok => new[] { krok.PlanStart, krok.PlanEnd, krok.RealStart, krok.RealEnd })
                    .ToList();
                var breakdownAxisStart = breakdownDates.Count > 0 ? breakdownDates.Min() : compactAxisStart;
                var breakdownAxisEnd = breakdownDates.Count > 0
                    ? new[] { breakdownDates.Max(), deadline, breakdownAxisStart }.Max()
                    : compactAxisEnd;
                var breakdownAxisDays = Math.Max(1, (breakdownAxisEnd - breakdownAxisStart).Days);
                ApplyProjectScheduleVisuals(kroky, compactAxisStart, compactAxisDays, breakdownAxisStart, breakdownAxisDays);

                var compactDeadlinePercent = ToAxisPercent(deadline, compactAxisStart, compactAxisDays);
                var compactTodayPercent = ToAxisPercent(today, compactAxisStart, compactAxisDays);
                var breakdownTodayPercent = ToAxisPercent(today, breakdownAxisStart, breakdownAxisDays);

                return new ProjektHarmonogramUkolViewModel
                {
                    ZaznamId = record.Id,
                    CisloZaznamu = record.CisloZaznamu,
                    CisloViditelne = record.CisloViditelne,
                    Nazev = record.Nazev,
                    KategorieKod = record.KategorieKod,
                    KategorieNazev = record.KategorieNazev,
                    TypUkoluKod = record.TypUkoluKod,
                    TypUkolu = record.TypUkolu,
                    Stav = record.Stav,
                    StavKod = record.StavKod,
                    SubsystemKod = record.AktualniSubsystemKod,
                    Subsystem = record.AktualniSubsystem,
                    Vlastnik = ownerDisplay,
                    VlastnikOrgKod = ownerOrgCode,
                    VlastnikId = record.AktualniVlastnikId,
                    IsAktivniStav = record.IsAktivniStav,
                    DatumZalozeni = record.DatumZalozeni.Date,
                    TerminUkonceni = deadline,
                    BaselineDokonceni = souhrn.BaselineDokonceni,
                    SkutecneDokonceni = souhrn.SkutecneDokonceni,
                    CelkoveTrvaniDni = souhrn.CelkoveTrvaniDni,
                    CelkovaOdchylkaDni = souhrn.CelkovaOdchylkaDni,
                    DelkaDoTerminuDni = Math.Max(0, (deadline - record.DatumZalozeni.Date).Days),
                    Stihame = souhrn.Stihame,
                    PrekroceniDni = souhrn.PrekroceniDni,
                    DelayBarvaHex = schema.DelayBarvaHex,
                    MaVizualniTrvani = hasVisualDuration,
                    Kroky = kroky,
                    CompactAxisStart = compactAxisStart,
                    CompactAxisEnd = compactAxisEnd,
                    BreakdownAxisStart = breakdownAxisStart,
                    BreakdownAxisEnd = breakdownAxisEnd,
                    CompactDeadlinePercent = compactDeadlinePercent,
                    CompactTodayPercent = compactTodayPercent,
                    BreakdownTodayPercent = breakdownTodayPercent,
                    FormatCompactDeadlinePercent = FormatPercent(compactDeadlinePercent),
                    FormatCompactTodayPercent = FormatPercent(compactTodayPercent),
                    FormatBreakdownTodayPercent = FormatPercent(breakdownTodayPercent),
                    CompactTodayTitle = $"Dnes: {today:dd.MM.yyyy}"
                };
            })
            .Where(x => x is not null)
            .Cast<ProjektHarmonogramUkolViewModel>()
            .ToList();
    }

    private static void ApplyProjectScheduleVisuals(
        IReadOnlyList<ProjektHarmonogramKrokViewModel> kroky,
        DateTime compactAxisStart,
        int compactAxisDays,
        DateTime breakdownAxisStart,
        int breakdownAxisDays)
    {
        var previousPlanRight = 0d;
        var previousActualRight = 0d;

        foreach (var krok in kroky)
        {
            var rawPlanLeft = ToAxisPercent(krok.PlanStart, compactAxisStart, compactAxisDays);
            var rawPlanRight = ToAxisPercent(krok.PlanEnd, compactAxisStart, compactAxisDays);
            var planLeft = Math.Max(previousPlanRight, rawPlanLeft);
            var planRight = Math.Max(planLeft, rawPlanRight);
            var planWidth = Math.Max(0d, planRight - planLeft);
            krok.CompactPlanLeftPercent = FormatPercent(planLeft);
            krok.CompactPlanWidthStyle = planWidth > 0d
                ? $"calc({FormatPercent(planWidth)}% + 1px)"
                : "0%";
            krok.CompactPlanTitle = $"{krok.Nazev}: plán {krok.PlanStart:dd.MM.yyyy} - {krok.PlanEnd:dd.MM.yyyy}";
            previousPlanRight = planRight;

            var rawActualLeft = ToAxisPercent(krok.RealStart, compactAxisStart, compactAxisDays);
            var rawActualRight = ToAxisPercent(krok.RealEnd, compactAxisStart, compactAxisDays);
            var actualLeft = Math.Max(previousActualRight, rawActualLeft);
            var actualRight = Math.Max(actualLeft, rawActualRight);
            var actualWidth = Math.Max(0d, actualRight - actualLeft);
            krok.CompactActualLeftPercent = FormatPercent(actualLeft);
            krok.CompactActualWidthStyle = actualWidth > 0d
                ? $"calc({FormatPercent(actualWidth)}% + 1px)"
                : "0%";
            krok.CompactActualTitle = $"{krok.Nazev}: skutečnost {krok.RealStart:dd.MM.yyyy} - {krok.RealEnd:dd.MM.yyyy}";
            previousActualRight = actualRight;

            var breakdownPlanLeft = ToAxisPercent(krok.PlanStart, breakdownAxisStart, breakdownAxisDays);
            var breakdownPlanRight = ToAxisPercent(krok.PlanEnd, breakdownAxisStart, breakdownAxisDays);
            var breakdownActualLeft = ToAxisPercent(krok.RealStart, breakdownAxisStart, breakdownAxisDays);
            var breakdownActualRight = ToAxisPercent(krok.RealEnd, breakdownAxisStart, breakdownAxisDays);
            krok.BreakdownPlanLeftPercent = FormatPercent(breakdownPlanLeft);
            krok.BreakdownPlanWidthPercent = FormatPercent(Math.Max(0d, breakdownPlanRight - breakdownPlanLeft));
            krok.BreakdownActualLeftPercent = FormatPercent(breakdownActualLeft);
            krok.BreakdownActualWidthPercent = FormatPercent(Math.Max(0d, breakdownActualRight - breakdownActualLeft));
            krok.HasBreakdownVisualDuration = krok.TrvaniDni > 0;
            krok.OffsetLabel = krok.OdchylkaDni > 0
                ? $"+{krok.OdchylkaDni} dnů"
                : $"{krok.OdchylkaDni} dnů";
            krok.OffsetCssClass = krok.OdchylkaDni > 0
                ? "late"
                : krok.OdchylkaDni < 0
                    ? "ahead"
                    : null;
        }
    }

    private static double ToAxisPercent(DateTime value, DateTime axisStart, int axisDays)
        => Math.Clamp(((value.Date - axisStart.Date).Days * 100d) / axisDays, 0d, 100d);

    private static string FormatPercent(double value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private async Task<List<ActiveProjectMembershipRow>> BuildActiveProjectMembershipRowsAsync(int projectId, CancellationToken ct)
    {
        var activeProjectRoleAssignments = await BuildActiveProjectRoleAssignmentsAsync(projectId, ct);
        var activeSubsystemRoleAssignments = await BuildActiveProjectSubsystemRoleAssignmentsAsync(projectId, ct);

        var roleFragments = activeProjectRoleAssignments
            .Select(item => new
            {
                item.OsobaId,
                item.Osoba,
                item.Email,
                item.Organizace,
                item.OrganizacniCelek,
                HasProjectRole = true,
                HasNonHostProjectRole = !Ci.Equals(item.RoleKod, ProjectRoleCodes.Host),
                HasHostRole = Ci.Equals(item.RoleKod, ProjectRoleCodes.Host),
                HasSubsystemRole = false,
                RoleLabel = item.RoleNazev
            })
            .Concat(activeSubsystemRoleAssignments.Select(item => new
            {
                item.OsobaId,
                item.Osoba,
                item.Email,
                Organizace = (string?)null,
                OrganizacniCelek = (string?)null,
                HasProjectRole = false,
                HasNonHostProjectRole = false,
                HasHostRole = false,
                HasSubsystemRole = true,
                RoleLabel = BuildSubsystemRoleLabel(item.RoleNazev, item.SubsystemKod, item.SubsystemNazev)
            }))
            .ToList();

        if (roleFragments.Count == 0)
        {
            return [];
        }

        var personIds = roleFragments.Select(item => item.OsobaId).Distinct().ToList();
        var people = await dbContext.Osoby.AsNoTracking()
            .Where(x => personIds.Contains(x.Id))
            .ToListAsync(ct);
        var organizations = await dbContext.CiselnikOrganizace.AsNoTracking().ToListAsync(ct);
        var orgUnits = await dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToListAsync(ct);
        var peopleById = people.ToDictionary(x => x.Id);
        var organizationsById = organizations.ToDictionary(x => x.Id);
        var orgUnitsById = orgUnits.ToDictionary(x => x.Id);

        return roleFragments
            .GroupBy(item => item.OsobaId)
            .Select(group =>
            {
                var first = group.First();
                var person = peopleById.GetValueOrDefault(group.Key);
                return new ActiveProjectMembershipRow(
                    group.Key,
                    first.Osoba,
                    first.Email,
                    first.Organizace ?? (person is null ? null : organizationsById.GetValueOrDefault(person.OrganizaceId)?.Nazev),
                    first.OrganizacniCelek ?? (person?.OrganizacniCelekId is int orgUnitId ? orgUnitsById.GetValueOrDefault(orgUnitId)?.Nazev : null),
                    group.Any(item => item.HasProjectRole),
                    group.Any(item => item.HasNonHostProjectRole),
                    group.Any(item => item.HasHostRole),
                    group.Any(item => item.HasSubsystemRole),
                    group.Select(item => item.RoleLabel)
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(Ci)
                        .OrderBy(item => item, StringComparer.CurrentCultureIgnoreCase)
                        .ToList());
            })
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectRoleGridRowViewModel>> BuildUnifiedActiveProjectRoleRowsAsync(int projectId, CancellationToken ct)
    {
        var membershipByPersonId = (await BuildActiveProjectMembershipRowsAsync(projectId, ct)).ToDictionary(item => item.OsobaId);
        var activeProjectRoleAssignments = await BuildActiveProjectRoleAssignmentsAsync(projectId, ct);
        var activeSubsystemRoleAssignments = await BuildActiveProjectSubsystemRoleAssignmentsAsync(projectId, ct);

        return activeProjectRoleAssignments
            .Select(item => new ProjectRoleGridRowViewModel
            {
                AssignmentId = item.ProjektRoleId,
                AssignmentKind = "PROJECT",
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                Email = item.Email,
                RoleKod = item.RoleKod,
                RoleNazev = item.RoleNazev,
                RoleTypeLabel = "Projektová",
                SubsystemKod = null,
                SubsystemNazev = null,
                Organizace = item.Organizace,
                OrganizacniCelek = item.OrganizacniCelek,
                DatumPrirazeni = item.DatumPrirazeni
            })
            .Concat(activeSubsystemRoleAssignments.Select(item =>
            {
                var member = membershipByPersonId.GetValueOrDefault(item.OsobaId);
                return new ProjectRoleGridRowViewModel
                {
                    AssignmentId = item.ProjektSubsystemRoleId,
                    AssignmentKind = "SUBSYSTEM",
                    OsobaId = item.OsobaId,
                    Osoba = item.Osoba,
                    Email = item.Email,
                    RoleKod = item.RoleKod,
                    RoleNazev = item.RoleNazev,
                    RoleTypeLabel = "Subsystémová",
                    SubsystemKod = item.SubsystemKod,
                    SubsystemNazev = item.SubsystemNazev,
                    Organizace = member?.Organizace,
                    OrganizacniCelek = member?.OrganizacniCelek,
                    DatumPrirazeni = item.DatumPrirazeni
                };
            }))
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => Ci.Equals(item.AssignmentKind, "PROJECT") ? 0 : 1)
            .ThenBy(item => item.SubsystemNazev ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectRoleHistoryGridRowViewModel>> BuildUnifiedProjectRoleHistoryRowsAsync(int projectId, CancellationToken ct)
    {
        var projectRoleHistory = await BuildProjectRoleHistoryAsync(projectId, ct);
        var projectSubsystemRoleHistory = await BuildProjectSubsystemRoleHistoryAsync(projectId, ct);

        return projectRoleHistory
            .Select(item => new ProjectRoleHistoryGridRowViewModel
            {
                AssignmentId = item.ProjektRoleId,
                AssignmentKind = "PROJECT",
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                RoleKod = item.RoleKod,
                RoleNazev = item.RoleNazev,
                RoleTypeLabel = "Projektová",
                SubsystemKod = null,
                SubsystemNazev = null,
                DatumPrirazeni = item.DatumPrirazeni,
                DatumOdebrani = item.DatumOdebrani
            })
            .Concat(projectSubsystemRoleHistory.Select(item => new ProjectRoleHistoryGridRowViewModel
            {
                AssignmentId = item.ProjektSubsystemRoleId,
                AssignmentKind = "SUBSYSTEM",
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                RoleKod = item.RoleKod,
                RoleNazev = item.RoleNazev,
                RoleTypeLabel = "Subsystémová",
                SubsystemKod = item.SubsystemKod,
                SubsystemNazev = item.SubsystemNazev,
                DatumPrirazeni = item.DatumPrirazeni,
                DatumOdebrani = item.DatumOdebrani
            }))
            .OrderByDescending(item => item.DatumOdebrani)
            .ThenBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => Ci.Equals(item.AssignmentKind, "PROJECT") ? 0 : 1)
            .ThenBy(item => item.SubsystemNazev ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectRoleAssignmentViewModel>> BuildActiveProjectRoleAssignmentsAsync(int projectId, CancellationToken ct)
    {
        var assignments = await dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var roles = (await dbContext.CiselnikRoliProjektu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);
        var organizations = await LoadOrganizationsByPeopleAsync(people.Values, ct);
        var orgUnits = await LoadOrgUnitsByPeopleAsync(people.Values, ct);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var role = roles.GetValueOrDefault(assignment.RoleId);
                return new ProjectRoleAssignmentViewModel
                {
                    ProjektRoleId = assignment.Id,
                    OsobaId = assignment.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(person),
                    Email = person?.Email?.Trim(),
                    RoleKod = role?.Kod ?? "-",
                    RoleNazev = role?.Nazev ?? "-",
                    Organizace = person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                    OrganizacniCelek = person?.OrganizacniCelekId is int cel ? orgUnits.GetValueOrDefault(cel)?.Nazev : null,
                    DatumPrirazeni = assignment.DatumPrirazeni
                };
            })
            .OrderBy(x => x.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectRoleHistoryItemViewModel>> BuildProjectRoleHistoryAsync(int projectId, CancellationToken ct)
    {
        var assignments = await dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);
        var roles = (await dbContext.CiselnikRoliProjektu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);

        return assignments
            .Where(x => x.DatumOdebrani.HasValue)
            .Select(assignment => new ProjectRoleHistoryItemViewModel
            {
                ProjektRoleId = assignment.Id,
                OsobaId = assignment.OsobaId,
                Osoba = BuildDisplayNameFromOsoba(people.GetValueOrDefault(assignment.OsobaId)),
                RoleKod = roles.GetValueOrDefault(assignment.RoleId)?.Kod ?? "-",
                RoleNazev = roles.GetValueOrDefault(assignment.RoleId)?.Nazev ?? "-",
                DatumPrirazeni = assignment.DatumPrirazeni,
                DatumOdebrani = assignment.DatumOdebrani
            })
            .OrderByDescending(x => x.DatumOdebrani)
            .ThenBy(x => x.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectSubsystemViewModel>> BuildActiveProjectSubsystemsAsync(int projectId, CancellationToken ct)
    {
        var mappings = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var subsystems = (await dbContext.Subsystemy.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);

        return mappings
            .Select(mapping =>
            {
                var subsystem = subsystems.GetValueOrDefault(mapping.SubsystemId);
                return new ProjectSubsystemViewModel
                {
                    ProjektSubsystemId = mapping.Id,
                    SubsystemId = mapping.SubsystemId,
                    Kod = subsystem?.Kod ?? "-",
                    Nazev = subsystem?.Nazev ?? "-",
                    DatumPrirazeni = mapping.DatumPrirazeni
                };
            })
            .OrderBy(x => x.Kod, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectSubsystemRoleAssignmentViewModel>> BuildActiveProjectSubsystemRoleAssignmentsAsync(int projectId, CancellationToken ct)
    {
        var projectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && !x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);
        var subsystems = (await dbContext.Subsystemy.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var roleById = (await dbContext.CiselnikRoliSubsystemu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var psById = projectSubsystems.ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var projektSubsystem = psById.GetValueOrDefault(assignment.ProjektSubsystemId);
                var subsystem = projektSubsystem is null ? null : subsystems.GetValueOrDefault(projektSubsystem.SubsystemId);
                var role = roleById.GetValueOrDefault(assignment.RoleSubsystemuId);
                return new ProjectSubsystemRoleAssignmentViewModel
                {
                    ProjektSubsystemRoleId = assignment.Id,
                    ProjektSubsystemId = assignment.ProjektSubsystemId,
                    OsobaId = assignment.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(person),
                    Email = person?.Email?.Trim(),
                    SubsystemKod = subsystem?.Kod ?? "-",
                    SubsystemNazev = subsystem?.Nazev ?? "-",
                    RoleKod = role?.Kod ?? "-",
                    RoleNazev = role?.Nazev ?? "-",
                    DatumPrirazeni = assignment.DatumPrirazeni
                };
            })
            .OrderBy(x => x.SubsystemNazev, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectSubsystemRoleHistoryItemViewModel>> BuildProjectSubsystemRoleHistoryAsync(int projectId, CancellationToken ct)
    {
        var projectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToListAsync(ct);
        var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && x.DatumOdebrani.HasValue)
            .ToListAsync(ct);
        var people = await LoadPeopleByIdsAsync(assignments.Select(x => x.OsobaId).Distinct(), ct);
        var subsystems = (await dbContext.Subsystemy.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var roleById = (await dbContext.CiselnikRoliSubsystemu.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var psById = projectSubsystems.ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var projektSubsystem = psById.GetValueOrDefault(assignment.ProjektSubsystemId);
                var subsystem = projektSubsystem is null ? null : subsystems.GetValueOrDefault(projektSubsystem.SubsystemId);
                return new ProjectSubsystemRoleHistoryItemViewModel
                {
                    ProjektSubsystemRoleId = assignment.Id,
                    OsobaId = assignment.OsobaId,
                    Osoba = BuildDisplayNameFromOsoba(people.GetValueOrDefault(assignment.OsobaId)),
                    SubsystemKod = subsystem?.Kod ?? "-",
                    SubsystemNazev = subsystem?.Nazev ?? "-",
                    RoleKod = roleById.GetValueOrDefault(assignment.RoleSubsystemuId)?.Kod ?? "-",
                    RoleNazev = roleById.GetValueOrDefault(assignment.RoleSubsystemuId)?.Nazev ?? "-",
                    DatumPrirazeni = assignment.DatumPrirazeni,
                    DatumOdebrani = assignment.DatumOdebrani
                };
            })
            .OrderByDescending(x => x.DatumOdebrani)
            .ToList();
    }

    private async Task<List<ProjectMemberCandidateViewModel>> BuildProjectMemberCandidatesAsync(CancellationToken ct)
    {
        var organizations = (await dbContext.CiselnikOrganizace.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var orgUnits = (await dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
        var people = await dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToListAsync(ct);

        return people
            .Select(person => new ProjectMemberCandidateViewModel
            {
                OsobaId = person.Id,
                Osoba = BuildDisplayName(person.Titul, person.Jmeno, person.Prijmeni, person.Id),
                Email = person.Email?.Trim(),
                Organizace = organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev,
                OrganizacniCelek = person.OrganizacniCelekId.HasValue ? orgUnits.GetValueOrDefault(person.OrganizacniCelekId.Value)?.Nazev : null
            })
            .ToList();
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

    private async Task<List<SpolupracovnikOptionViewModel>> BuildRecordOwnerCandidatesAsync(int projectId, int? selectedOwnerId, CancellationToken ct)
    {
        var rows = (await BuildActiveProjectMembershipRowsAsync(projectId, ct))
            .Select(item => new SpolupracovnikOptionViewModel
            {
                OsobaId = item.OsobaId,
                Osoba = item.Osoba,
                Email = item.Email,
                Organizace = item.Organizace,
                OrganizacniCelek = item.OrganizacniCelek
            })
            .ToList();

        if (selectedOwnerId.HasValue && rows.All(item => item.OsobaId != selectedOwnerId.Value))
        {
            var organizations = (await dbContext.CiselnikOrganizace.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
            var orgUnits = (await dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToListAsync(ct)).ToDictionary(x => x.Id);
            var selectedOwner = await dbContext.Osoby.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == selectedOwnerId.Value, ct);
            if (selectedOwner is not null)
            {
                rows.Add(new SpolupracovnikOptionViewModel
                {
                    OsobaId = selectedOwner.Id,
                    Osoba = BuildDisplayName(selectedOwner.Titul, selectedOwner.Jmeno, selectedOwner.Prijmeni, selectedOwner.Id),
                    Email = selectedOwner.Email?.Trim(),
                    Organizace = organizations.GetValueOrDefault(selectedOwner.OrganizaceId)?.Nazev,
                    OrganizacniCelek = selectedOwner.OrganizacniCelekId.HasValue
                        ? orgUnits.GetValueOrDefault(selectedOwner.OrganizacniCelekId.Value)?.Nazev
                        : null
                });
            }
        }

        return rows
            .DistinctBy(item => item.OsobaId)
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<ProjectSubsystemOptionViewModel>> BuildProjectSubsystemOptionsAsync(int projectId, CancellationToken ct)
    {
        return (await BuildActiveProjectSubsystemsAsync(projectId, ct))
            .Select(item => new ProjectSubsystemOptionViewModel
            {
                ProjektSubsystemId = item.ProjektSubsystemId,
                SubsystemId = item.SubsystemId,
                Kod = item.Kod,
                Nazev = item.Nazev,
                Label = string.IsNullOrWhiteSpace(item.Kod) ? item.Nazev : $"{item.Kod} - {item.Nazev}"
            })
            .ToList();
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

    private async Task<Dictionary<int, int>> BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(int projectId, CancellationToken ct)
    {
        var leadRoleId = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (!leadRoleId.HasValue)
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
                && x.RoleSubsystemuId == leadRoleId.Value)
            .ToListAsync(ct);

        return assignments
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.OsobaId).First());
    }

    private async Task<List<ProjectSubsystemViewModel>> BuildRecordEditorProjectSubsystemsAsync(int projectId, int currentSubsystemId, CancellationToken ct)
    {
        var mappings = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .Select(x => new
            {
                x.Id,
                x.SubsystemId
            })
            .ToListAsync(ct);

        var subsystemIds = mappings
            .Select(x => x.SubsystemId)
            .Append(currentSubsystemId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var subsystemRows = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => subsystemIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Kod,
                x.Nazev
            })
            .ToListAsync(ct);
        var subsystemsById = subsystemRows.ToDictionary(x => x.Id);

        return subsystemIds
            .Select(subsystemId =>
            {
                var subsystem = subsystemsById.GetValueOrDefault(subsystemId);
                var mapping = mappings.FirstOrDefault(x => x.SubsystemId == subsystemId);

                return new ProjectSubsystemViewModel
                {
                    ProjektSubsystemId = mapping?.Id ?? 0,
                    SubsystemId = subsystemId,
                    Kod = subsystem?.Kod ?? "-",
                    Nazev = subsystem?.Nazev ?? "-",
                    DatumPrirazeni = DateTime.MinValue
                };
            })
            .OrderBy(x => x.Kod, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<int>> ResolveLeadEquivalentOsobaIdsAsync(int projectId, int subsystemId, CancellationToken ct)
        => (await BuildLeadEquivalentOsobaIdsByProjectSubsystemAsync(projectId, ct)).GetValueOrDefault(subsystemId, []);

    private async Task<ZaznamEditViewModel> BuildZaznamEditForEntityAsync(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber = null,
        bool? projectUsesMeetingIdentifier = null,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions = null,
        CancellationToken ct = default)
    {
        var categories = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking().OrderBy(x => x.Nazev).ToListAsync(ct);
        var taskStates = await dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToListAsync(ct);
        var taskTypes = await dbContext.CiselnikTypuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToListAsync(ct);
        var projectSubsystems = await BuildRecordEditorProjectSubsystemsAsync(record.ProjektId, record.SubsystemId, ct);
        var defaultOwnerBySubsystemId = await BuildDefaultOwnerOsobaIdsByProjectSubsystemAsync(record.ProjektId, ct);
        var ownerCandidates = await BuildRecordOwnerCandidatesAsync(record.ProjektId, record.VlastnikId, ct);
        var collaborationCandidates = await BuildRecordOwnerCandidatesAsync(record.ProjektId, null, ct);

        var extTypes = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().OrderBy(x => x.Kod).ToListAsync(ct);
        var vyzvyById = (await dbContext.CiselnikVyzvy.AsNoTracking()
                .OrderBy(x => x.Kod)
                .Select(x => new
                {
                    x.Id,
                    x.Kod
                })
                .ToListAsync(ct))
            .ToDictionary(x => x.Id, x => x.Kod);

        var projectUsesMeetingNumbering = projectUsesMeetingIdentifier
            ?? await dbContext.Projekty.AsNoTracking()
                .Where(x => x.Id == record.ProjektId)
                .Select(x => (bool?)x.PouzivatIdentJednani)
                .FirstOrDefaultAsync(ct)
            ?? false;
        var meetingOptions = openMeetingOptions ?? await BuildOpenMeetingOptionsAsync(await BuildJednaniListAsync(record.ProjektId, ct), ct);
        var selectedMeetingId = forceMeetingIdForNumber
            ?? (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting ? record.CisloJednaniZdrojId : null);

        var externalLinks = isCreate
            ? new List<ExterniOdkazEditViewModel>()
            : (await dbContext.ZaznamExterniOdkazy.AsNoTracking()
                    .Where(x => x.ZaznamId == record.Id)
                    .OrderBy(x => x.Id)
                    .ToListAsync(ct))
                .Select(x => new ExterniOdkazEditViewModel
                {
                    Id = x.Id,
                    Typ = extTypes.FirstOrDefault(et => et.Id == x.TypOdkazuId)?.Kod ?? string.Empty,
                    Cislo = x.Cislo,
                    PredpokladanaCena = x.PredpokladanaCena,
                    Vyzva = x.Vyzva.HasValue ? vyzvyById.GetValueOrDefault(x.Vyzva.Value) : null,
                    DatumObjednani = x.DatumObjednani,
                    PlanDodani = x.PlanDodani,
                    DatumDodani = x.DatumDodani,
                    DatumPrevzeti = x.DatumPrevzeti
                })
                .ToList();

        var selectedCollaborationIds = isCreate
            ? new List<int>()
            : await dbContext.ZaznamSpoluprace.AsNoTracking().Where(x => x.ZaznamId == record.Id).Select(x => x.OsobaId).ToListAsync(ct);

        var selectedCategory = categories.FirstOrDefault(x => x.Id == record.KategorieId);
        var isTaskCategory = IsTaskCategory(selectedCategory?.Kod, selectedCategory?.Nazev);
        var harmonogramSchema = isCreate
            ? await harmonogramService.GetActiveHarmonogramSchemaAsync(ct)
            : await harmonogramService.GetSchemaForRecordAsync(record, ct);
        var harmonogramTypy = harmonogramSchema.Kroky;
        var allowedTypeIds = harmonogramTypy
            .SelectMany(x => new[] { x.TrvaniTypId, x.ZpozdeniTypId })
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var harmonogramValues = (!isCreate && isTaskCategory && allowedTypeIds.Count > 0)
            ? (await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                    .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
                    .ToListAsync(ct))
                .ToDictionary(x => x.TypId, x => x.HodnotaInt)
            : new Dictionary<int, int>();
        var harmonogramKroky = harmonogramService.BuildHarmonogramVypocetPublic(record.DatumZalozeni, harmonogramTypy, harmonogramValues);
        var harmonogramSouhrn = harmonogramService.BuildHarmonogramSouhrn(harmonogramKroky, record.DatumUkonceni);

        return new ZaznamEditViewModel
        {
            Id = record.Id,
            CisloZaznamu = record.CisloZaznamu,
            CisloViditelne = ResolveVisibleRecordNumber(record),
            ProjektId = record.ProjektId,
            IsCreate = isCreate,
            PouzivatIdentJednani = projectUsesMeetingNumbering,
            MaDostupneJednaniProCislo = meetingOptions.Count > 0,
            MuzeDoplnitIdentifikatorJednani = !isCreate && projectUsesMeetingNumbering && record.CisloViditelneTyp != RecordDisplayNumberTypeMeeting,
            JednaniIdProCislo = selectedMeetingId,
            JednaniProCisloOptions = meetingOptions,
            Nazev = record.Nazev,
            Cil = record.Cil ?? string.Empty,
            Kategorie = selectedCategory?.Nazev ?? string.Empty,
            Popis = record.Popis ?? string.Empty,
            TypUkolu = record.AktualniTypUkoluId.HasValue ? taskTypes.FirstOrDefault(x => x.Id == record.AktualniTypUkoluId.Value)?.Nazev : null,
            Stav = record.StavUkoluId.HasValue ? taskStates.FirstOrDefault(x => x.Id == record.StavUkoluId.Value)?.Nazev ?? string.Empty : string.Empty,
            KategorieZaznamu = categories.Select(x => x.Nazev).ToList(),
            StavyUkolu = taskStates.Select(x => x.Nazev).ToList(),
            TypyUkolu = taskTypes.Select(x => x.Nazev).ToList(),
            DatumZalozeni = record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystemy = projectSubsystems.Select(x => new SubsystemOptionViewModel
            {
                Kod = x.Kod,
                Nazev = x.Nazev,
                DefaultOwnerOsobaId = defaultOwnerBySubsystemId.GetValueOrDefault(x.SubsystemId)
            }).ToList(),
            Subsystem = projectSubsystems.FirstOrDefault(x => x.SubsystemId == record.SubsystemId)?.Nazev ?? string.Empty,
            Vlastnici = ownerCandidates.Select(x => new LookupOptionViewModel
            {
                Value = x.OsobaId.ToString(CultureInfo.InvariantCulture),
                Label = string.IsNullOrWhiteSpace(x.Email)
                    ? x.Osoba
                    : $"{x.Osoba} <{textNormalizer.NormalizeEmail(x.Email)}>"
            }).ToList(),
            VlastnikId = record.VlastnikId,
            JeUkolKategorie = isTaskCategory,
            HarmonogramDelayBarvaHex = harmonogramSchema.DelayBarvaHex,
            HarmonogramKroky = harmonogramKroky.Select(krok => new HarmonogramKrokEditViewModel
            {
                KrokIndex = krok.KrokIndex,
                Nazev = krok.Nazev,
                BarvaHex = krok.BarvaHex,
                TrvaniTypId = krok.TrvaniTypId,
                ZpozdeniTypId = krok.ZpozdeniTypId,
                TrvaniDni = krok.TrvaniDni,
                OdchylkaDni = krok.ZpozdeniDni,
                BaselineDatum = krok.BaselineDatum,
                SkutecneDatum = krok.PosunuteDatum
            }).ToList(),
            HarmonogramSouhrn = harmonogramSouhrn,
            DostupniVlastnici = ownerCandidates,
            DostupniSpolupracovnici = collaborationCandidates,
            VybraniSpolupracovniciIds = selectedCollaborationIds,
            ExterniVazby = externalLinks,
            TypyExternichOdkazu = extTypes.Select(x => x.Kod).ToList(),
            Vyzvy = vyzvyById.Values.ToList()
        };
    }

    private List<JednaniOptionViewModel> BuildOpenMeetingOptions(IReadOnlyList<JednaniListItemViewModel> meetings)
    {
        return meetings
            .Where(IsMeetingOpenForRecordNumbering)
            .OrderByDescending(x => x.CisloJednani)
            .Select(x => new JednaniOptionViewModel
            {
                Id = x.Id,
                Label = $"Jednání č. {x.CisloJednani} ({x.Datum:dd.MM.yyyy})",
                Datum = x.Datum.Date
            })
            .ToList();
    }

    private Task<List<JednaniOptionViewModel>> BuildOpenMeetingOptionsAsync(IReadOnlyList<JednaniListItemViewModel> meetings, CancellationToken ct)
        => Task.FromResult(BuildOpenMeetingOptions(meetings));

    private static int? ResolveSelectedMeetingIdForNumber(
        IReadOnlyList<JednaniOptionViewModel> openMeetingOptions,
        int? contextMeetingId)
    {
        if (contextMeetingId.HasValue && openMeetingOptions.Any(x => x.Id == contextMeetingId.Value))
        {
            return contextMeetingId.Value;
        }

        return openMeetingOptions.FirstOrDefault()?.Id;
    }

    private bool IsMeetingOpenForRecordNumbering(JednaniListItemViewModel meeting)
    {
        return !string.IsNullOrWhiteSpace(meeting.StavKod)
            && !Ci.Equals(meeting.StavKod, "CLOSED")
            && !meeting.Stav.Contains("uzav", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(meeting.UzamklOsoba);
    }

    private static bool IsMeetingReadOnly(JednaniEntity? meeting, CiselnikStavuJednaniEntity? status)
        => MeetingStatePolicy.IsReadOnly(meeting, status);

    private static bool IsTaskCategory(string? categoryCode, string? categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryCode)
            && (Ci.Equals(categoryCode.Trim(), "U") || Ci.Equals(categoryCode.Trim(), "UKOL")))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        return categoryName.Contains("úkol", StringComparison.OrdinalIgnoreCase)
            || categoryName.Contains("ukol", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSubsystemRoleLabel(string roleName, string? subsystemCode, string? subsystemName)
    {
        var subsystemLabel = !string.IsNullOrWhiteSpace(subsystemCode)
            ? subsystemCode
            : subsystemName;

        return string.IsNullOrWhiteSpace(subsystemLabel)
            ? roleName
            : $"{roleName} ({subsystemLabel})";
    }

    private async Task<int> GetNextCisloZaznamuTransactionalAsync(int projektId, CancellationToken ct)
    {
        var existingValues = await dbContext.ProjektoveZaznamy
            .FromSqlRaw("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE projekt_id = {0}", projektId)
            .Select(x => x.CisloZaznamu)
            .ToListAsync(ct);

        return RecordNumberAllocator.FindLowestAvailablePositive(existingValues);
    }

    private async Task<int> AllocateMeetingOrderTransactionalAsync(int projektId, int cisloJednani, CancellationToken ct)
    {
        var existingOrders = await dbContext.ProjektoveZaznamy
            .FromSqlRaw(
                "SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE projekt_id = {0} AND cislo_viditelne_typ = {1} AND cislo_viditelne_a = {2}",
                projektId,
                RecordDisplayNumberTypeMeeting,
                cisloJednani)
            .Select(x => x.CisloViditelneB)
            .ToListAsync(ct);

        return RecordNumberAllocator.FindLowestAvailablePositive(existingOrders);
    }

    private Task<int?> ResolveProjectRoleIdAsync(string value, CancellationToken ct)
        => dbContext.CiselnikRoliProjektu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

    private async Task WriteAuditAsync(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue, CancellationToken ct)
    {
        dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        });
        await dbContext.SaveChangesAsync(ct);
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
        return string.IsNullOrWhiteSpace(displayName) ? $"Uživatel #{id}" : displayName;
    }

    private string BuildDisplayNameFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id);
    }

    private string BuildInlinePersonLabel(string? titul, string jmeno, string prijmeni, string? email, int id)
    {
        var displayName = BuildDisplayName(titul, jmeno, prijmeni, id);
        var normalizedEmail = textNormalizer.NormalizeEmail(email);
        return string.IsNullOrWhiteSpace(normalizedEmail)
            ? displayName
            : $"{displayName} <{normalizedEmail}>";
    }

    private string BuildInlinePersonLabelFromOsoba(OsobaEntity? osoba)
    {
        if (osoba is null)
        {
            return "-";
        }

        return BuildInlinePersonLabel(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Email, osoba.Id);
    }

    private static string? ExtractServiceDeskTicketId(string? externalNumber)
    {
        if (string.IsNullOrWhiteSpace(externalNumber))
        {
            return null;
        }

        var digits = new string(externalNumber.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static string? BuildServiceDeskUrl(string? ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return null;
        }

        return $"https://servicedesk.fis.acr/Hotline/Ticket/Details/{ticketId}";
    }
}
