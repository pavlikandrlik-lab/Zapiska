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
