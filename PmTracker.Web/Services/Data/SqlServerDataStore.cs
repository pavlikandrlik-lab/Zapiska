using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Export;
using PmTracker.Web.Modules.Settings;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Services.Data;

public sealed class SqlServerDataStore : IPmTrackerDataStore, IProjectDetailComposition, IDictionariesQueriesComposition, IRecordEditorQueriesComposition, IRecordWriteCommandsComposition, IDictionariesCommandsComposition
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;
    private const string HarmonogramKrokyCiselnikKey = "harmonogram-kroky";
    private const int HarmonogramDelayColorPseudoRowId = 0;
    private const string HarmonogramDelayColorPseudoKod = "DELAY_COLOR";
    private const string DefaultDelayBarvaHex = "#DC2626";
    private const byte RecordDisplayNumberTypeIncrement = 0;
    private const byte RecordDisplayNumberTypeMeeting = 1;
    private static readonly IReadOnlyDictionary<int, int> EmptyIntMap = new Dictionary<int, int>();
    private static readonly (int Poradi, string Kod, string Nazev, string BarvaHex)[] DefaultHarmonogramKroky =
    [
        (1, "HS01_DURATION", "1. příprava zadání dodavateli", "#EF4444"),
        (2, "HS02_DURATION", "2. konzultace termínů s dodavatelem před vytvořením zadání", "#F97316"),
        (3, "HS03_DURATION", "3. odeslání zadání dodavateli", "#F59E0B"),
        (4, "HS04_DURATION", "4. dodání návrhu řešení", "#84CC16"),
        (5, "HS05_DURATION", "5. vypořádání připomínek", "#22C55E"),
        (6, "HS06_DURATION", "6. odeslání požadavku na výrobu", "#14B8A6"),
        (7, "HS07_DURATION", "7. dodání funkcionality dodavatelem", "#06B6D4"),
        (8, "HS08_DURATION", "8. připomínkování", "#3B82F6"),
        (9, "HS09_DURATION", "9. testování", "#6366F1"),
        (10, "HS10_DURATION", "10. nasazení do provozu", "#8B5CF6"),
        (11, "HS11_DURATION", "11. fakturace", "#D946EF")
    ];

    private readonly PmTrackerDbContext _dbContext;
    private readonly ITextNormalizer _textNormalizer;
    private readonly IRichTextContentService _richTextContentService;
    private readonly IPersonIdentityMatcher _personIdentityMatcher;
    private readonly ICommentAuthorizationPolicy _commentAuthorizationPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly IExportTemplateUseCase _exportTemplateUseCase;
    private readonly IUserAuthorizationSnapshotBuilder _userAuthorizationSnapshotBuilder;
    private readonly ISettingsAuthzQueries _settingsAuthzQueries;
    private readonly ISettingsAuthzCommands _settingsAuthzCommands;
    private readonly IRecordCommentCommandsUseCase _recordCommentCommandsUseCase;
    private readonly IProfilePageQueriesUseCase _profilePageQueriesUseCase;
    private readonly IMeetingListQueriesUseCase _meetingListQueriesUseCase;
    private readonly IMeetingDetailQueriesUseCase _meetingDetailQueriesUseCase;
    private readonly IMeetingWriteCommandsUseCase _meetingWriteCommandsUseCase;
    private readonly IProjectListQueriesUseCase _projectListQueriesUseCase;
    private readonly IProjectCommandsUseCase _projectCommandsUseCase;
    private readonly IProjectDetailQueriesUseCase _projectDetailQueriesUseCase;
    private readonly IPeoplePageQueriesUseCase _peoplePageQueriesUseCase;
    private readonly IDictionariesQueriesUseCase _dictionariesQueriesUseCase;
    private readonly IDictionariesCommandsUseCase _dictionariesCommandsUseCase;
    private readonly IPersonCommandsUseCase _personCommandsUseCase;
    private readonly IRecordEditorQueriesUseCase _recordEditorQueriesUseCase;
    private readonly IRecordWriteCommandsUseCase _recordWriteCommandsUseCase;
    private readonly IProjectAssignmentCommandsUseCase _projectAssignmentCommandsUseCase;

    private sealed record HarmonogramTypPar(
        int KrokIndex,
        string Kod,
        string Nazev,
        string BarvaHex,
        int TrvaniTypId,
        int ZpozdeniTypId);

    private sealed record HarmonogramSchemaDefinition(
        int Verze,
        string DelayBarvaHex,
        IReadOnlyList<HarmonogramTypPar> Kroky);

    private sealed record HarmonogramSchemaCloneResult(
        HarmonogramSablonaEntity SourceSchema,
        HarmonogramSablonaEntity NewSchema,
        IReadOnlyDictionary<int, HarmonogramTypEntity> ClonedBySourceId,
        IReadOnlyList<HarmonogramTypEntity> ClonedRows);
    private sealed record HarmonogramVypocetKroku(
        int KrokIndex,
        string Kod,
        string Nazev,
        string BarvaHex,
        int TrvaniTypId,
        int ZpozdeniTypId,
        int TrvaniDni,
        int ZpozdeniDni,
        DateTime PlanStartDatum,
        DateTime BaselineDatum,
        DateTime RealStartDatum,
        DateTime PosunuteDatum);
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

    public SqlServerDataStore(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IRichTextContentService richTextContentService,
        IPersonIdentityMatcher personIdentityMatcher,
        ICommentAuthorizationPolicy commentAuthorizationPolicy,
        TimeProvider timeProvider,
        IExportTemplateUseCase exportTemplateUseCase,
        IUserAuthorizationSnapshotBuilder userAuthorizationSnapshotBuilder,
        ISettingsAuthzQueries settingsAuthzQueries,
        ISettingsAuthzCommands settingsAuthzCommands,
        IRecordCommentCommandsUseCase recordCommentCommandsUseCase,
        IProfilePageQueriesUseCase profilePageQueriesUseCase,
        IMeetingListQueriesUseCase meetingListQueriesUseCase,
        IMeetingDetailQueriesUseCase meetingDetailQueriesUseCase,
        IMeetingWriteCommandsUseCase meetingWriteCommandsUseCase,
        IProjectListQueriesUseCase projectListQueriesUseCase,
        IProjectCommandsUseCase projectCommandsUseCase,
        IProjectDetailQueriesUseCase projectDetailQueriesUseCase,
        IPeoplePageQueriesUseCase peoplePageQueriesUseCase,
        IDictionariesQueriesUseCase dictionariesQueriesUseCase,
        IDictionariesCommandsUseCase dictionariesCommandsUseCase,
        IPersonCommandsUseCase personCommandsUseCase,
        IRecordEditorQueriesUseCase recordEditorQueriesUseCase,
        IRecordWriteCommandsUseCase recordWriteCommandsUseCase,
        IProjectAssignmentCommandsUseCase projectAssignmentCommandsUseCase)
    {
        _dbContext = dbContext;
        _textNormalizer = textNormalizer;
        _richTextContentService = richTextContentService;
        _personIdentityMatcher = personIdentityMatcher;
        _commentAuthorizationPolicy = commentAuthorizationPolicy;
        _timeProvider = timeProvider;
        _exportTemplateUseCase = exportTemplateUseCase;
        _userAuthorizationSnapshotBuilder = userAuthorizationSnapshotBuilder;
        _settingsAuthzQueries = settingsAuthzQueries;
        _settingsAuthzCommands = settingsAuthzCommands;
        _recordCommentCommandsUseCase = recordCommentCommandsUseCase;
        _profilePageQueriesUseCase = profilePageQueriesUseCase;
        _meetingListQueriesUseCase = meetingListQueriesUseCase;
        _meetingDetailQueriesUseCase = meetingDetailQueriesUseCase;
        _meetingWriteCommandsUseCase = meetingWriteCommandsUseCase;
        _projectListQueriesUseCase = projectListQueriesUseCase;
        _projectCommandsUseCase = projectCommandsUseCase;
        _projectDetailQueriesUseCase = projectDetailQueriesUseCase;
        _peoplePageQueriesUseCase = peoplePageQueriesUseCase;
        _dictionariesQueriesUseCase = dictionariesQueriesUseCase;
        _dictionariesCommandsUseCase = dictionariesCommandsUseCase;
        _personCommandsUseCase = personCommandsUseCase;
        _recordEditorQueriesUseCase = recordEditorQueriesUseCase;
        _recordWriteCommandsUseCase = recordWriteCommandsUseCase;
        _projectAssignmentCommandsUseCase = projectAssignmentCommandsUseCase;
    }

    private DateTime GetLocalNow()
        => _timeProvider.GetLocalNow().LocalDateTime;

    public CurrentUserContextViewModel BuildCurrentUserContext(string? asProfile)
    {
        var osobaId = ResolveAsProfileToOsobaId(asProfile);

        var osoba = _dbContext.Osoby
            .AsNoTracking()
            .Where(x => x.Id == osobaId)
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.Email,
                x.GuidAd,
                x.OrganizacniCelekId
            })
            .FirstOrDefault();

        if (osoba is null)
        {
            throw new InvalidOperationException($"Osoba '{asProfile}' nebyla v DB nalezena.");
        }

        var authzSnapshot = _userAuthorizationSnapshotBuilder.Build(osoba.Id);

        var orgUnit = _dbContext.CiselnikOrganizacniCelky.AsNoTracking()
            .Where(x => x.Id == osoba.OrganizacniCelekId)
            .Select(x => new { x.Kod, x.Nazev })
            .FirstOrDefault();

        return new CurrentUserContextViewModel
        {
            OsobaId = osoba.Id,
            Jmeno = osoba.Jmeno,
            Prijmeni = osoba.Prijmeni,
            DisplayName = BuildDisplayName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni, osoba.Id),
            Email = osoba.Email?.Trim() ?? string.Empty,
            OrganizacniCelekKod = string.IsNullOrWhiteSpace(orgUnit?.Kod) ? null : orgUnit.Kod.Trim(),
            OrganizacniCelek = orgUnit?.Nazev ?? "-",
            IsSuperAdmin = authzSnapshot.IsSuperAdmin,
            RoleKody = authzSnapshot.RoleKody,
            VisibleProjectIds = authzSnapshot.VisibleProjectIds,
            DeletedProjectIds = authzSnapshot.DeletedProjectIds,
            PermissionGrants = authzSnapshot.PermissionGrants
        };
    }

    public bool ProjektExists(int id)
        => _dbContext.Projekty.AsNoTracking().Any(x => x.Id == id);

    public IReadOnlyList<ProjektListItemViewModel> BuildProjektyList()
        => _projectListQueriesUseCase.BuildProjektyList();

    public ProjektDetailViewModel BuildProjektDetail(int id)
        => _projectDetailQueriesUseCase.BuildProjektDetail(id, this);

    public ZaznamEditViewModel BuildZaznamEdit(int id)
        => _recordEditorQueriesUseCase.BuildZaznamEdit(id, this);

    public ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId = null)
        => _recordEditorQueriesUseCase.BuildZaznamCreate(projektId, jednaniId, this);

    public DeleteRecordModalViewModel BuildDeleteRecordModal(int projektId, int zaznamId)
        => _recordEditorQueriesUseCase.BuildDeleteRecordModal(projektId, zaznamId);

    public int GetNextCisloZaznamu(int projektId)
    {
        var existingValues = _dbContext.ProjektoveZaznamy
            .AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .Select(x => x.CisloZaznamu)
            .ToList();

        return RecordNumberAllocator.FindLowestAvailablePositive(existingValues);
    }

    public IReadOnlyList<JednaniProjektListItemViewModel> BuildJednaniOverview()
        => _meetingListQueriesUseCase.BuildJednaniOverview();

    public IReadOnlyList<JednaniListItemViewModel> BuildJednaniList(int projektId)
        => _meetingListQueriesUseCase.BuildJednaniList(projektId);

    public JednaniDetailViewModel BuildJednaniDetail(int id)
        => _meetingDetailQueriesUseCase.BuildJednaniDetail(id);

    IReadOnlyList<ProjectSubsystemViewModel> IProjectDetailComposition.BuildActiveProjectSubsystems(int projectId)
        => BuildActiveProjectSubsystems(projectId);

    IReadOnlyList<ZaznamCardViewModel> IProjectDetailComposition.BuildRecordCardsForProject(int projectId)
        => BuildRecordCardsForProject(projectId);

    IReadOnlyList<ProjektHarmonogramUkolViewModel> IProjectDetailComposition.BuildProjectScheduleRows(IReadOnlyList<ZaznamCardViewModel> records)
        => BuildProjectScheduleRows(records);

    IReadOnlyList<JednaniListItemViewModel> IProjectDetailComposition.BuildJednaniList(int projectId)
        => BuildJednaniList(projectId);

    IReadOnlyList<ProjectRoleGridRowViewModel> IProjectDetailComposition.BuildUnifiedActiveProjectRoleRows(int projectId)
        => BuildUnifiedActiveProjectRoleRows(projectId);

    IReadOnlyList<ProjectRoleHistoryGridRowViewModel> IProjectDetailComposition.BuildUnifiedProjectRoleHistoryRows(int projectId)
        => BuildUnifiedProjectRoleHistoryRows(projectId);

    IReadOnlyList<ProjectMemberCandidateViewModel> IProjectDetailComposition.BuildProjectMemberCandidates()
        => BuildProjectMemberCandidates();

    IReadOnlyList<ProjectSubsystemOptionViewModel> IProjectDetailComposition.BuildProjectSubsystemOptions(int projectId)
        => BuildProjectSubsystemOptions(projectId);

    IReadOnlyList<JednaniOptionViewModel> IProjectDetailComposition.BuildOpenMeetingOptions(IReadOnlyList<JednaniListItemViewModel> meetings)
        => BuildOpenMeetingOptions(meetings);

    public OsobyIndexViewModel BuildOsoby()
        => _peoplePageQueriesUseCase.BuildOsoby();

    public ProfilPageViewModel BuildProfilPage(CurrentUserContextViewModel currentUser, int? projektId)
        => _profilePageQueriesUseCase.BuildProfilPage(currentUser, projektId);

    public CiselnikyDashboardViewModel BuildCiselnikyDashboard(string? id, CurrentUserContextViewModel currentUser)
        => _dictionariesQueriesUseCase.BuildCiselnikyDashboard(id, currentUser, this);

    public CiselnikDetailViewModel BuildCiselnikDetail(string id, CurrentUserContextViewModel currentUser)
        => _dictionariesQueriesUseCase.BuildCiselnikDetail(id, currentUser, this);

    CiselnikDetailViewModel IDictionariesQueriesComposition.BuildHarmonogramKrokyCiselnikDetail(string key, bool canChangeLockState)
        => BuildHarmonogramKrokyCiselnikDetail(key, canChangeLockState);

    int IDictionariesQueriesComposition.CountHarmonogramCatalogRows()
        => CountHarmonogramCatalogRows();

    ZaznamEditViewModel IRecordEditorQueriesComposition.BuildZaznamEditForEntity(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber,
        bool? projectUsesMeetingIdentifier,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions)
        => BuildZaznamEditForEntity(record, isCreate, forceMeetingIdForNumber, projectUsesMeetingIdentifier, openMeetingOptions);

    IReadOnlyList<ProjectSubsystemViewModel> IRecordEditorQueriesComposition.BuildActiveProjectSubsystems(int projectId)
        => BuildActiveProjectSubsystems(projectId);

    IReadOnlyList<JednaniListItemViewModel> IRecordEditorQueriesComposition.BuildJednaniList(int projectId)
        => BuildJednaniList(projectId);

    IReadOnlyList<JednaniOptionViewModel> IRecordEditorQueriesComposition.BuildOpenMeetingOptions(IReadOnlyList<JednaniListItemViewModel> meetings)
        => BuildOpenMeetingOptions(meetings);

    int? IRecordEditorQueriesComposition.ResolveSelectedMeetingIdForNumber(
        IReadOnlyList<JednaniOptionViewModel> openMeetingOptions,
        int? contextMeetingId)
        => ResolveSelectedMeetingIdForNumber(openMeetingOptions, contextMeetingId);

    IReadOnlyDictionary<int, int> IRecordEditorQueriesComposition.BuildDefaultOwnerOsobaIdsByProjectSubsystem(int projectId)
        => BuildDefaultOwnerOsobaIdsByProjectSubsystem(projectId);

    int IRecordEditorQueriesComposition.EnsurePersistedActiveHarmonogramSchemaVersion()
        => EnsurePersistedActiveHarmonogramSchemaVersion();

    int IRecordEditorQueriesComposition.GetNextCisloZaznamu(int projectId)
        => GetNextCisloZaznamu(projectId);

    int IRecordWriteCommandsComposition.GetNextCisloZaznamuTransactional(int projektId)
        => GetNextCisloZaznamuTransactional(projektId);

    int IRecordWriteCommandsComposition.AllocateMeetingOrderTransactional(int projektId, int cisloJednani)
        => AllocateMeetingOrderTransactional(projektId, cisloJednani);

    IReadOnlyList<SpolupracovnikOptionViewModel> IRecordWriteCommandsComposition.BuildRecordOwnerCandidates(int projectId, int? selectedOwnerId)
        => BuildRecordOwnerCandidates(projectId, selectedOwnerId);

    int IRecordWriteCommandsComposition.EnsurePersistedActiveHarmonogramSchemaVersion()
        => EnsurePersistedActiveHarmonogramSchemaVersion();

    IReadOnlyList<RecordScheduleTypeDefinition> IRecordWriteCommandsComposition.ResolveScheduleTypeDefinitionsForRecord(ProjektovyZaznamEntity record)
        => BuildRecordScheduleTypeDefinitions(GetSchemaForRecord(record));

    IReadOnlyList<RecordScheduleTypeDefinition> IRecordWriteCommandsComposition.ResolveScheduleTypeDefinitionsForSchemaVersion(int schemaVersion)
        => BuildRecordScheduleTypeDefinitions(GetSchemaForRecord(schemaVersion));

    IReadOnlyList<int> IRecordWriteCommandsComposition.ResolveLeadEquivalentOsobaIds(int projectId, int subsystemId)
        => ResolveLeadEquivalentOsobaIds(projectId, subsystemId);

    public NastaveniDashboardViewModel BuildNastaveniDashboard(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
        => _settingsAuthzQueries.BuildNastaveniDashboard(section, currentUser, userId, projektId);

    public NastaveniPanelViewModel BuildNastaveniPanel(string? section, CurrentUserContextViewModel currentUser, int? userId, int? projektId)
        => _settingsAuthzQueries.BuildNastaveniPanel(section, currentUser, userId, projektId);

    public PdfExportTemplateViewModel BuildProjectPrintTemplate(
        int projektId,
        CurrentUserContextViewModel currentUser,
        bool autoPrint)
    {
        return _exportTemplateUseCase.BuildProjectTemplate(projektId, currentUser, autoPrint);
    }

    public PdfExportTemplateViewModel BuildMeetingPrintTemplate(
        int jednaniId,
        CurrentUserContextViewModel currentUser,
        bool autoPrint)
    {
        return _exportTemplateUseCase.BuildMeetingTemplate(jednaniId, currentUser, autoPrint);
    }

    public PdfExportTemplateViewModel BuildTaskPrintTemplate(
        int projektId,
        int zaznamId,
        CurrentUserContextViewModel currentUser,
        bool autoPrint)
    {
        return _exportTemplateUseCase.BuildTaskTemplate(projektId, zaznamId, currentUser, autoPrint);
    }

    public int SaveProject(SaveProjectCommand command, CurrentUserContextViewModel currentUser)
        => _projectCommandsUseCase.SaveProject(command, currentUser);

    public void SoftDeleteProject(SoftDeleteProjectCommand command, CurrentUserContextViewModel currentUser)
        => _projectCommandsUseCase.SoftDeleteProject(command, currentUser);

    public int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser)
        => _recordWriteCommandsUseCase.SaveRecord(command, currentUser, this);

    public void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser)
        => _recordWriteCommandsUseCase.DeleteRecord(command, currentUser);

    public void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser)
        => _recordWriteCommandsUseCase.AssignMeetingIdentifier(command, currentUser, this);

    public void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser)
        => _recordCommentCommandsUseCase.AddComment(command, currentUser);

    public void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser)
        => _recordCommentCommandsUseCase.UpdateComment(command, currentUser);

    public void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser)
        => _recordCommentCommandsUseCase.DeleteComment(command, currentUser);

    public int SaveMeeting(SaveMeetingCommand command, CurrentUserContextViewModel currentUser)
        => _meetingWriteCommandsUseCase.SaveMeeting(command, currentUser);

    public void DeleteMeeting(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser)
        => _meetingWriteCommandsUseCase.DeleteMeeting(command, currentUser);

    public void SaveMeetingStatus(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser)
        => _meetingWriteCommandsUseCase.SaveMeetingStatus(command, currentUser);

    public void SaveMeetingNote(SaveMeetingNoteCommand command, CurrentUserContextViewModel currentUser)
        => _meetingWriteCommandsUseCase.SaveMeetingNote(command, currentUser);

    public void SaveAttendance(SaveAttendanceCommand command, CurrentUserContextViewModel currentUser)
        => _meetingWriteCommandsUseCase.SaveAttendance(command, currentUser);

    public void AddMeetingParticipant(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser)
        => _meetingWriteCommandsUseCase.AddMeetingParticipant(command, currentUser);

    public void AssignProjectRole(AssignProjectRoleCommand command, CurrentUserContextViewModel currentUser)
        => _projectAssignmentCommandsUseCase.AssignProjectRole(command, currentUser);

    public void DeactivateProjectRole(DeactivateProjectRoleCommand command, CurrentUserContextViewModel currentUser)
        => _projectAssignmentCommandsUseCase.DeactivateProjectRole(command, currentUser);

    public void AssignProjectSubsystem(AssignProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
        => _projectAssignmentCommandsUseCase.AssignProjectSubsystem(command, currentUser);

    public void DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, CurrentUserContextViewModel currentUser)
        => _projectAssignmentCommandsUseCase.DeactivateProjectSubsystem(command, currentUser);

    public void AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
        => _projectAssignmentCommandsUseCase.AssignProjectSubsystemRole(command, currentUser);

    public void DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, CurrentUserContextViewModel currentUser)
        => _projectAssignmentCommandsUseCase.DeactivateProjectSubsystemRole(command, currentUser);

    public void SaveTeamMember(SaveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
    {
        if (!command.OsobaId.HasValue || command.OsobaId.Value <= 0)
        {
            throw new InvalidOperationException("Vyberte osobu z nabídky.");
        }

        var osobaId = command.OsobaId.Value;
        var roleId = ResolveProjectRoleId(command.Role)
            ?? throw new InvalidOperationException($"Role '{command.Role}' nebyla nalezena.");

        var existing = _dbContext.ObsazeniProjektu
            .FirstOrDefault(x => x.ProjektId == command.ProjektId && x.OsobaId == osobaId);
        if (existing is null)
        {
            _dbContext.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
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

        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{osobaId}", "upsert", null, JsonSerializer.Serialize(command));
    }

    public void RemoveTeamMember(RemoveTeamMemberCommand command, CurrentUserContextViewModel currentUser)
    {
        var rows = _dbContext.ObsazeniProjektu
            .Where(x => x.ProjektId == command.ProjektId && x.OsobaId == command.OsobaId)
            .ToList();
        if (rows.Count == 0)
        {
            return;
        }

        _dbContext.ObsazeniProjektu.RemoveRange(rows);
        _dbContext.SaveChanges();
        WriteAudit(currentUser.OsobaId, "obsazeni_projektu", $"{command.ProjektId}:{command.OsobaId}", "delete", null, null);
    }

    public int SaveManualPerson(SaveManualPersonCommand command, CurrentUserContextViewModel currentUser)
        => _personCommandsUseCase.SaveManualPerson(command, currentUser);

    public int SaveAdPerson(SaveAdPersonCommand command, CurrentUserContextViewModel currentUser)
        => _personCommandsUseCase.SaveAdPerson(command, currentUser);

    public void DeletePerson(DeletePersonCommand command, CurrentUserContextViewModel currentUser)
        => _personCommandsUseCase.DeletePerson(command, currentUser);

    public void SaveCiselnikRow(SaveCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
        => _dictionariesCommandsUseCase.SaveCiselnikRow(command, currentUser, this);

    public void DeleteCiselnikRow(DeleteCiselnikRowCommand command, CurrentUserContextViewModel currentUser)
        => _dictionariesCommandsUseCase.DeleteCiselnikRow(command, currentUser, this);

    void IDictionariesCommandsComposition.SaveHarmonogramStepRow(SaveCiselnikRowCommand command)
        => SaveHarmonogramStepRow(command);

    void IDictionariesCommandsComposition.DeleteHarmonogramStepRow(DeleteCiselnikRowCommand command)
        => DeleteHarmonogramStepRow(command);

    public void SaveUserRoleAssignment(SaveUserRoleAssignmentCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.SaveUserRoleAssignment(command, currentUser);

    public void SaveUserRolesForUser(SaveUserRolesForUserCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.SaveUserRolesForUser(command, currentUser);

    public void SaveAuthzRole(SaveAuthzRoleCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.SaveAuthzRole(command, currentUser);

    public void ToggleAuthzRole(ToggleAuthzRoleCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.ToggleAuthzRole(command, currentUser);

    public void SaveAuthzPermission(SaveAuthzPermissionCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.SaveAuthzPermission(command, currentUser);

    public void ToggleAuthzPermission(ToggleAuthzPermissionCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.ToggleAuthzPermission(command, currentUser);

    public void SaveRolePermission(SaveRolePermissionCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.SaveRolePermission(command, currentUser);

    public void DeleteRolePermission(DeleteRolePermissionCommand command, CurrentUserContextViewModel currentUser)
        => _settingsAuthzCommands.DeleteRolePermission(command, currentUser);

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

    private List<ZaznamCardViewModel> BuildRecordCardsForProject(int projectId)
    {
        var records = _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToList();
        records = OrderRecordsByVisibleNumber(records).ToList();
        var recordIds = records.Select(record => record.Id).ToArray();

        var categories = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionary(x => x.Id);
        var taskTypes = _dbContext.CiselnikTypuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var taskStates = _dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var leadEquivalentOsobaIdsBySubsystem = BuildLeadEquivalentOsobaIdsByProjectSubsystem(projectId);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        var ownerHistoryByRecord = _dbContext.ZaznamHistorieVlastnik.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var termHistoryByRecord = _dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var subsystemHistoryByRecord = _dbContext.ZaznamHistorieSubsystem.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var typeHistoryByRecord = _dbContext.ZaznamHistorieZmenTypu.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderBy(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var extTypeById = _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().ToDictionary(x => x.Id);
        var vyzvaById = _dbContext.CiselnikVyzvy.AsNoTracking().ToDictionary(x => x.Id);
        var externalByRecord = _dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderBy(x => x.Id)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
        var collaborationByRecord = _dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var comments = _dbContext.Vyjadreni.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderBy(x => x.Id)
            .ToList();

        var meetings = _dbContext.Jednani.AsNoTracking().ToDictionary(x => x.Id);
        var meetingStates = _dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);

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

    private List<ProjektHarmonogramUkolViewModel> BuildProjectScheduleRows(IReadOnlyList<ZaznamCardViewModel> records)
    {
        var taskRecords = records
            .Where(x => x.JeUkol)
            .ToList();

        if (taskRecords.Count == 0)
        {
            return new List<ProjektHarmonogramUkolViewModel>();
        }

        var taskRecordIds = taskRecords.Select(x => x.Id).ToList();
        var harmonogramByRecord = _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => taskRecordIds.Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(item => item.TypId, item => item.HodnotaInt));
        var schemaCache = new Dictionary<int, HarmonogramSchemaDefinition>();
        var ownerIds = taskRecords.Select(x => x.AktualniVlastnikId).Distinct().ToList();
        var ownerById = _dbContext.Osoby.AsNoTracking()
            .Where(x => ownerIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Titul,
                x.Jmeno,
                x.Prijmeni,
                x.OrganizacniCelekId
            })
            .ToDictionary(x => x.Id);
        var ownerOrgUnitIds = ownerById.Values
            .Where(x => x.OrganizacniCelekId.HasValue)
            .Select(x => x.OrganizacniCelekId!.Value)
            .Distinct()
            .ToList();
        var ownerOrgCodes = _dbContext.CiselnikOrganizacniCelky.AsNoTracking()
            .Where(x => ownerOrgUnitIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Kod })
            .ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Kod) ? null : x.Kod.Trim());

        return taskRecords
            .Select(record =>
            {
                var deadline = (record.AktualniTermin ?? record.DatumZalozeni).Date;
                var schema = GetSchemaForRecord(record.HarmonogramSablonaVerze, schemaCache);
                var harmonogramHodnoty = harmonogramByRecord.TryGetValue(record.Id, out var harmonogramValues)
                    ? harmonogramValues
                    : EmptyIntMap;
                var vypocet = BuildHarmonogramVypocet(record.DatumZalozeni, schema.Kroky, harmonogramHodnoty);
                var souhrn = BuildHarmonogramSouhrn(vypocet, deadline);
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
                    Kroky = vypocet
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
                        .ToList()
                };
            })
            .Where(x => x is not null)
            .Cast<ProjektHarmonogramUkolViewModel>()
            .ToList();
    }

    private HarmonogramSchemaDefinition GetSchemaForRecord(
        ProjektovyZaznamEntity record,
        IDictionary<int, HarmonogramSchemaDefinition>? cache = null)
        => GetSchemaForRecord(record.HarmonogramSablonaVerze, cache);

    private HarmonogramSchemaDefinition GetSchemaForRecord(
        int schemaVersion,
        IDictionary<int, HarmonogramSchemaDefinition>? cache = null)
    {
        if (schemaVersion <= 0)
        {
            var activeSchema = GetActiveHarmonogramSchema();
            if (cache is not null && activeSchema.Verze > 0 && !cache.ContainsKey(activeSchema.Verze))
            {
                cache[activeSchema.Verze] = activeSchema;
            }

            return activeSchema;
        }

        if (cache is not null && cache.TryGetValue(schemaVersion, out var cached))
        {
            return cached;
        }

        var loaded = LoadHarmonogramSchema(schemaVersion);
        if (cache is not null && loaded.Verze > 0)
        {
            cache[loaded.Verze] = loaded;
        }

        return loaded;
    }

    private static IReadOnlyList<RecordScheduleTypeDefinition> BuildRecordScheduleTypeDefinitions(HarmonogramSchemaDefinition schema)
    {
        return schema.Kroky
            .Select(step => new RecordScheduleTypeDefinition(step.TrvaniTypId, step.ZpozdeniTypId))
            .ToList();
    }

    private HarmonogramSchemaDefinition GetActiveHarmonogramSchema()
    {
        try
        {
            var schema = _dbContext.HarmonogramSablony.AsNoTracking()
                .OrderByDescending(x => x.IsAktivni)
                .ThenByDescending(x => x.Verze)
                .FirstOrDefault();
            if (schema is null)
            {
                return BuildFallbackSchemaDefinition();
            }

            var steps = LoadHarmonogramTypy(schema.Verze);
            if (steps.Count == 0)
            {
                var fallback = BuildFallbackSchemaDefinition(schema.Verze, schema.DelayBarvaHex);
                return fallback;
            }

            return new HarmonogramSchemaDefinition(
                schema.Verze,
                NormalizeHexColor(schema.DelayBarvaHex, DefaultDelayBarvaHex),
                steps);
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return BuildFallbackSchemaDefinition();
        }
    }

    private int EnsurePersistedActiveHarmonogramSchemaVersion()
    {
        var activeSchema = _dbContext.HarmonogramSablony
            .OrderByDescending(x => x.IsAktivni)
            .ThenByDescending(x => x.Verze)
            .FirstOrDefault(x => x.IsAktivni);

        if (activeSchema is null)
        {
            activeSchema = new HarmonogramSablonaEntity
            {
                DelayBarvaHex = DefaultDelayBarvaHex,
                IsAktivni = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = null
            };
            _dbContext.HarmonogramSablony.Add(activeSchema);
            _dbContext.SaveChanges();
        }

        var hasRows = _dbContext.CiselnikHarmonogramTypu
            .Any(x => x.SablonaVerze == activeSchema.Verze);

        if (!hasRows)
        {
            var defaultRows = BuildDefaultHarmonogramTypeRows(activeSchema.Verze);
            _dbContext.CiselnikHarmonogramTypu.AddRange(defaultRows);
            _dbContext.SaveChanges();
            NormalizeHarmonogramSchemaRows(activeSchema.Verze);
            _dbContext.SaveChanges();
        }

        return activeSchema.Verze;
    }

    private HarmonogramSchemaDefinition LoadHarmonogramSchema(int schemaVersion)
    {
        if (schemaVersion <= 0)
        {
            return GetActiveHarmonogramSchema();
        }

        try
        {
            var schema = _dbContext.HarmonogramSablony.AsNoTracking()
                .FirstOrDefault(x => x.Verze == schemaVersion);
            if (schema is null)
            {
                return GetActiveHarmonogramSchema();
            }

            var steps = LoadHarmonogramTypy(schemaVersion);
            if (steps.Count == 0)
            {
                return BuildFallbackSchemaDefinition(schema.Verze, schema.DelayBarvaHex);
            }

            return new HarmonogramSchemaDefinition(
                schema.Verze,
                NormalizeHexColor(schema.DelayBarvaHex, DefaultDelayBarvaHex),
                steps);
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return BuildFallbackSchemaDefinition(schemaVersion);
        }
    }

    private List<HarmonogramTypPar> LoadHarmonogramTypy(int schemaVersion)
    {
        List<HarmonogramTypEntity> typRows;
        try
        {
            typRows = _dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == schemaVersion)
                .OrderBy(x => x.KrokPoradi)
                .ThenBy(x => x.JeZpozdeni)
                .ThenBy(x => x.Id)
                .ToList();
        }
        catch (Exception ex) when (IsMissingHarmonogramCatalogSchema(ex))
        {
            return new List<HarmonogramTypPar>();
        }

        if (typRows.Count == 0)
        {
            return new List<HarmonogramTypPar>();
        }

        var durationRows = typRows
            .Where(x => !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();
        var delayRows = typRows
            .Where(x => x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();

        var result = new List<HarmonogramTypPar>(durationRows.Count);
        var krokIndex = 1;
        foreach (var duration in durationRows)
        {
            var delayType = delayRows.FirstOrDefault(x => x.KrokKey == duration.KrokKey)
                ?? delayRows.FirstOrDefault(x => x.KrokPoradi == duration.KrokPoradi);

            result.Add(new HarmonogramTypPar(
                krokIndex,
                string.IsNullOrWhiteSpace(duration.Kod) ? $"STEP_{krokIndex:00}" : duration.Kod.Trim(),
                string.IsNullOrWhiteSpace(duration.Nazev) ? $"Krok {krokIndex}" : duration.Nazev.Trim(),
                NormalizeHexColor(duration.BarvaHex, ResolveDefaultStepColor(krokIndex)),
                duration.Id,
                delayType?.Id ?? 0));
            krokIndex += 1;
        }

        return result;
    }

    private static HarmonogramSchemaDefinition BuildFallbackSchemaDefinition(int version = 0, string? delayBarvaHex = null)
    {
        return new HarmonogramSchemaDefinition(
            version,
            NormalizeHexColor(delayBarvaHex, DefaultDelayBarvaHex),
            DefaultHarmonogramKroky
                .Select(step => new HarmonogramTypPar(step.Poradi, step.Kod, step.Nazev, step.BarvaHex, 0, 0))
                .ToList());
    }

    private static List<HarmonogramTypEntity> BuildDefaultHarmonogramTypeRows(int schemaVersion)
    {
        var rows = new List<HarmonogramTypEntity>(DefaultHarmonogramKroky.Length * 2);

        foreach (var step in DefaultHarmonogramKroky)
        {
            var stepKey = Guid.NewGuid();
            rows.Add(new HarmonogramTypEntity
            {
                Kod = step.Kod,
                Nazev = step.Nazev,
                Hodnota = step.Poradi,
                IsLocked = true,
                SablonaVerze = schemaVersion,
                KrokKey = stepKey,
                KrokPoradi = step.Poradi,
                JeZpozdeni = false,
                BarvaHex = step.BarvaHex
            });
            rows.Add(new HarmonogramTypEntity
            {
                Kod = step.Kod.Replace("_DURATION", "_DELAY", StringComparison.OrdinalIgnoreCase),
                Nazev = $"{step.Nazev} - zpoždění",
                Hodnota = 100 + step.Poradi,
                IsLocked = true,
                SablonaVerze = schemaVersion,
                KrokKey = stepKey,
                KrokPoradi = step.Poradi,
                JeZpozdeni = true,
                BarvaHex = null
            });
        }

        return rows;
    }

    private static string ResolveDefaultStepColor(int stepIndex)
    {
        var matched = DefaultHarmonogramKroky.FirstOrDefault(step => step.Poradi == stepIndex);
        if (matched.Poradi > 0 && !string.IsNullOrWhiteSpace(matched.BarvaHex))
        {
            return matched.BarvaHex;
        }

        return DefaultHarmonogramKroky.Length > 0 ? DefaultHarmonogramKroky[0].BarvaHex : "#94A3B8";
    }

    private static bool IsValidHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Length != 7 || normalized[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < normalized.Length; i += 1)
        {
            var c = normalized[i];
            var isDigit = c >= '0' && c <= '9';
            var isLowerHex = c >= 'a' && c <= 'f';
            var isUpperHex = c >= 'A' && c <= 'F';
            if (!isDigit && !isLowerHex && !isUpperHex)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeHexColor(string? value, string fallback)
    {
        if (!IsValidHexColor(value))
        {
            return fallback.ToUpperInvariant();
        }

        return value!.Trim().ToUpperInvariant();
    }

    private static bool IsMissingHarmonogramCatalogSchema(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && (sqlException.Number == 208 || sqlException.Number == 207))
            {
                return true;
            }
        }

        return false;
    }

    private static List<HarmonogramVypocetKroku> BuildHarmonogramVypocet(
        DateTime datumZalozeni,
        IReadOnlyList<HarmonogramTypPar> harmonogramTypy,
        IReadOnlyDictionary<int, int>? harmonogramHodnoty)
    {
        var definitions = (harmonogramTypy.Count == 0
                ? DefaultHarmonogramKroky
                    .Select(step => new HarmonogramTypPar(step.Poradi, step.Kod, step.Nazev, step.BarvaHex, 0, 0))
                    .ToList()
                : harmonogramTypy.OrderBy(x => x.KrokIndex).ToList())
            .Select(type => new ScheduleTimelineStepDefinition
            {
                StepIndex = type.KrokIndex,
                Code = type.Kod,
                Name = type.Nazev,
                ColorHex = NormalizeHexColor(type.BarvaHex, ResolveDefaultStepColor(type.KrokIndex)),
                DurationTypeId = type.TrvaniTypId,
                OffsetTypeId = type.ZpozdeniTypId
            })
            .ToList();
        var computation = ScheduleTimelineCalculator.Compute(datumZalozeni, definitions, harmonogramHodnoty);

        return computation.Steps
            .Select(step => new HarmonogramVypocetKroku(
                step.StepIndex,
                step.Code,
                step.Name,
                step.ColorHex,
                step.DurationTypeId,
                step.OffsetTypeId,
                step.DurationDays,
                step.OffsetDays,
                step.PlanStartDate,
                step.PlanEndDate,
                step.ActualStartDate,
                step.ActualEndDate))
            .ToList();
    }

    private static HarmonogramSouhrnViewModel BuildHarmonogramSouhrn(
        IReadOnlyList<HarmonogramVypocetKroku> kroky,
        DateTime terminUkolu)
    {
        var summary = ScheduleTimelineCalculator.Summarize(
            new ScheduleTimelineComputation
            {
                Steps = kroky
                    .Select(step => new ScheduleTimelineStepResult
                    {
                        StepIndex = step.KrokIndex,
                        Code = step.Kod,
                        Name = step.Nazev,
                        ColorHex = step.BarvaHex,
                        DurationTypeId = step.TrvaniTypId,
                        OffsetTypeId = step.ZpozdeniTypId,
                        DurationDays = step.TrvaniDni,
                        OffsetDays = step.ZpozdeniDni,
                        PlanStartDate = step.PlanStartDatum,
                        PlanEndDate = step.BaselineDatum,
                        ActualStartDate = step.RealStartDatum,
                        ActualEndDate = step.PosunuteDatum
                    })
                    .ToList(),
                TotalDurationDays = kroky.Sum(x => x.TrvaniDni),
                TotalOffsetDays = kroky.Sum(x => x.ZpozdeniDni)
            },
            terminUkolu);

        return new HarmonogramSouhrnViewModel
        {
            BaselineDokonceni = summary.BaselineCompletion,
            SkutecneDokonceni = summary.ActualCompletion,
            TerminUkolu = summary.Deadline,
            CelkoveTrvaniDni = summary.TotalDurationDays,
            CelkovaOdchylkaDni = summary.TotalOffsetDays,
            Stihame = summary.IsOnTrack,
            PrekroceniDni = Math.Max(0, summary.OverrunDays)
        };
    }

    private List<ActiveProjectMembershipRow> BuildActiveProjectMembershipRows(int projectId)
    {
        var activeProjectRoleAssignments = BuildActiveProjectRoleAssignments(projectId);
        var activeSubsystemRoleAssignments = BuildActiveProjectSubsystemRoleAssignments(projectId);

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
        var people = _dbContext.Osoby.AsNoTracking()
            .Where(x => personIds.Contains(x.Id))
            .ToDictionary(x => x.Id);
        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);

        return roleFragments
            .GroupBy(item => item.OsobaId)
            .Select(group =>
            {
                var first = group.First();
                var person = people.GetValueOrDefault(group.Key);
                return new ActiveProjectMembershipRow(
                    group.Key,
                    first.Osoba,
                    first.Email,
                    first.Organizace ?? (person is null ? null : organizations.GetValueOrDefault(person.OrganizaceId)?.Nazev),
                    first.OrganizacniCelek ?? (person?.OrganizacniCelekId is int orgUnitId ? orgUnits.GetValueOrDefault(orgUnitId)?.Nazev : null),
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

    private List<ProjectRoleGridRowViewModel> BuildUnifiedActiveProjectRoleRows(int projectId)
    {
        var membershipByPersonId = BuildActiveProjectMembershipRows(projectId).ToDictionary(item => item.OsobaId);

        return BuildActiveProjectRoleAssignments(projectId)
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
            .Concat(BuildActiveProjectSubsystemRoleAssignments(projectId).Select(item =>
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

    private List<ProjectRoleHistoryGridRowViewModel> BuildUnifiedProjectRoleHistoryRows(int projectId)
    {
        return BuildProjectRoleHistory(projectId)
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
            .Concat(BuildProjectSubsystemRoleHistory(projectId).Select(item => new ProjectRoleHistoryGridRowViewModel
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

    private List<ProjectRoleAssignmentViewModel> BuildActiveProjectRoleAssignments(int projectId)
    {
        var assignments = _dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var roles = _dbContext.CiselnikRoliProjektu.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);
        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);

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

    private List<ProjectRoleHistoryItemViewModel> BuildProjectRoleHistory(int projectId)
    {
        var assignments = _dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToList();
        var roles = _dbContext.CiselnikRoliProjektu.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

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

    private List<ProjectSubsystemViewModel> BuildActiveProjectSubsystems(int projectId)
    {
        var mappings = _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);

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

    private List<ProjectSubsystemRoleAssignmentViewModel> BuildActiveProjectSubsystemRoleAssignments(int projectId)
    {
        var projectSubsystems = _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && !x.DatumOdebrani.HasValue)
            .ToList();
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var roleById = _dbContext.CiselnikRoliSubsystemu.AsNoTracking().ToDictionary(x => x.Id);
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

    private List<ProjectSubsystemRoleHistoryItemViewModel> BuildProjectSubsystemRoleHistory(int projectId)
    {
        var projectSubsystems = _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .ToList();
        var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
        var assignments = _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && x.DatumOdebrani.HasValue)
            .ToList();
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var roleById = _dbContext.CiselnikRoliSubsystemu.AsNoTracking().ToDictionary(x => x.Id);
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

    private List<ProjectMemberCandidateViewModel> BuildProjectMemberCandidates()
    {
        var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
        var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking()
            .OrderBy(x => x.Prijmeni)
            .ThenBy(x => x.Jmeno)
            .ToList();

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

    private List<SpolupracovnikOptionViewModel> BuildRecordOwnerCandidates(int projectId, int? selectedOwnerId)
    {
        var rows = BuildActiveProjectMembershipRows(projectId)
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
            var organizations = _dbContext.CiselnikOrganizace.AsNoTracking().ToDictionary(x => x.Id);
            var orgUnits = _dbContext.CiselnikOrganizacniCelky.AsNoTracking().ToDictionary(x => x.Id);
            var selectedOwner = _dbContext.Osoby.AsNoTracking().FirstOrDefault(x => x.Id == selectedOwnerId.Value);
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

    private List<ProjectSubsystemOptionViewModel> BuildProjectSubsystemOptions(int projectId)
    {
        return BuildActiveProjectSubsystems(projectId)
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

    private Dictionary<int, List<int>> BuildLeadEquivalentOsobaIdsByProjectSubsystem(int projectId)
    {
        var leadRoleIds = _dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead || x.Kod == SubsystemRoleCodes.DeputyLead)
            .Select(x => x.Id)
            .ToHashSet();
        if (leadRoleIds.Count == 0)
        {
            return new Dictionary<int, List<int>>();
        }

        var activeProjectSubsystems = _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();

        return _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && leadRoleIds.Contains(x.RoleSubsystemuId))
            .ToList()
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.OsobaId).Distinct().OrderBy(x => x).ToList());
    }

    private Dictionary<int, int> BuildDefaultOwnerOsobaIdsByProjectSubsystem(int projectId)
    {
        var leadRoleId = _dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        if (!leadRoleId.HasValue)
        {
            return new Dictionary<int, int>();
        }

        var activeProjectSubsystems = _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();

        return _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && x.RoleSubsystemuId == leadRoleId.Value)
            .ToList()
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.OsobaId).First());
    }

    private List<ProjectSubsystemViewModel> BuildRecordEditorProjectSubsystems(int projectId, int currentSubsystemId)
    {
        var mappings = _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .Select(x => new
            {
                x.Id,
                x.SubsystemId
            })
            .ToList();

        var subsystemIds = mappings
            .Select(x => x.SubsystemId)
            .Append(currentSubsystemId)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var subsystemsById = _dbContext.Subsystemy.AsNoTracking()
            .Where(x => subsystemIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Kod,
                x.Nazev
            })
            .ToDictionary(x => x.Id);

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

    private Dictionary<int, int> BuildRecordEditorDefaultOwnerOsobaIds(int projectId)
    {
        var leadRoleId = _dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == SubsystemRoleCodes.Lead)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
        if (!leadRoleId.HasValue)
        {
            return new Dictionary<int, int>();
        }

        var activeProjectSubsystems = _dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .Select(x => new
            {
                x.Id,
                x.SubsystemId
            })
            .ToList();
        var activeProjectSubsystemIds = activeProjectSubsystems.Select(x => x.Id).ToHashSet();

        return _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .Where(x => activeProjectSubsystemIds.Contains(x.ProjektSubsystemId)
                && !x.DatumOdebrani.HasValue
                && x.RoleSubsystemuId == leadRoleId.Value)
            .Select(x => new
            {
                x.ProjektSubsystemId,
                x.OsobaId
            })
            .ToList()
            .GroupBy(x => activeProjectSubsystems.First(ps => ps.Id == x.ProjektSubsystemId).SubsystemId)
            .ToDictionary(group => group.Key, group => group.Select(x => x.OsobaId).First());
    }

    private List<int> ResolveLeadEquivalentOsobaIds(int projectId, int subsystemId)
        => BuildLeadEquivalentOsobaIdsByProjectSubsystem(projectId).GetValueOrDefault(subsystemId, []);

    private ZaznamEditViewModel BuildZaznamEditForEntity(
        ProjektovyZaznamEntity record,
        bool isCreate,
        int? forceMeetingIdForNumber = null,
        bool? projectUsesMeetingIdentifier = null,
        IReadOnlyList<JednaniOptionViewModel>? openMeetingOptions = null)
    {
        var categories = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().OrderBy(x => x.Nazev).ToList();
        var taskStates = _dbContext.CiselnikStavuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToList();
        var taskTypes = _dbContext.CiselnikTypuUkolu.AsNoTracking().OrderBy(x => x.Nazev).ToList();
        var projectSubsystems = BuildRecordEditorProjectSubsystems(record.ProjektId, record.SubsystemId);
        var defaultOwnerBySubsystemId = BuildRecordEditorDefaultOwnerOsobaIds(record.ProjektId);
        var ownerCandidates = BuildRecordOwnerCandidates(record.ProjektId, record.VlastnikId);
        var collaborationCandidates = BuildRecordOwnerCandidates(record.ProjektId, null);

        var extTypes = _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().OrderBy(x => x.Kod).ToList();
        var vyzvyById = _dbContext.CiselnikVyzvy.AsNoTracking()
            .OrderBy(x => x.Kod)
            .Select(x => new
            {
                x.Id,
                x.Kod
            })
            .ToDictionary(x => x.Id, x => x.Kod);

        var projectUsesMeetingNumbering = projectUsesMeetingIdentifier
            ?? _dbContext.Projekty.AsNoTracking()
                .Where(x => x.Id == record.ProjektId)
                .Select(x => (bool?)x.PouzivatIdentJednani)
                .FirstOrDefault()
            ?? false;
        var meetingOptions = openMeetingOptions ?? BuildOpenMeetingOptions(BuildJednaniList(record.ProjektId));
        var selectedMeetingId = forceMeetingIdForNumber
            ?? (record.CisloViditelneTyp == RecordDisplayNumberTypeMeeting ? record.CisloJednaniZdrojId : null);

        var externalLinks = isCreate
            ? new List<ExterniOdkazEditViewModel>()
            : _dbContext.ZaznamExterniOdkazy.AsNoTracking().Where(x => x.ZaznamId == record.Id)
                .OrderBy(x => x.Id)
                .ToList()
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
            : _dbContext.ZaznamSpoluprace.AsNoTracking().Where(x => x.ZaznamId == record.Id).Select(x => x.OsobaId).ToList();

        var selectedCategory = categories.FirstOrDefault(x => x.Id == record.KategorieId);
        var isTaskCategory = IsTaskCategory(selectedCategory?.Kod, selectedCategory?.Nazev);
        var harmonogramSchema = isCreate
            ? GetActiveHarmonogramSchema()
            : GetSchemaForRecord(record);
        var harmonogramTypy = harmonogramSchema.Kroky;
        var allowedTypeIds = harmonogramTypy
            .SelectMany(x => new[] { x.TrvaniTypId, x.ZpozdeniTypId })
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var harmonogramValues = (!isCreate && isTaskCategory && allowedTypeIds.Count > 0)
            ? _dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
                .Where(x => x.ZaznamId == record.Id && allowedTypeIds.Contains(x.TypId))
                .ToDictionary(x => x.TypId, x => x.HodnotaInt)
            : new Dictionary<int, int>();
        var harmonogramKroky = BuildHarmonogramVypocet(record.DatumZalozeni, harmonogramTypy, harmonogramValues);
        var harmonogramSouhrn = BuildHarmonogramSouhrn(harmonogramKroky, record.DatumUkonceni);

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
                    : $"{x.Osoba} <{_textNormalizer.NormalizeEmail(x.Email)}>"
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

    private CiselnikDetailViewModel BuildHarmonogramKrokyCiselnikDetail(string key, bool canChangeLockState)
    {
        var schema = GetActiveHarmonogramSchema();
        var rows = schema.Kroky
            .OrderBy(x => x.KrokIndex)
            .Select(step => new CiselnikRadekViewModel
            {
                Id = step.TrvaniTypId,
                Kod = step.Kod,
                Nazev = step.Nazev,
                IsLocked = false,
                CanChangeLockState = canChangeLockState,
                CanEdit = true,
                CanDelete = true,
                HodnotyNavic = new[] { step.BarvaHex },
                HodnotyNavicRaw = new[] { step.BarvaHex }
            })
            .ToList();

        rows.Add(new CiselnikRadekViewModel
        {
            Id = HarmonogramDelayColorPseudoRowId,
            Kod = HarmonogramDelayColorPseudoKod,
            Nazev = "Globální barva skutečnosti",
            IsLocked = true,
            CanChangeLockState = canChangeLockState,
            CanEdit = true,
            CanDelete = false,
            HodnotyNavic = new[] { schema.DelayBarvaHex },
            HodnotyNavicRaw = new[] { schema.DelayBarvaHex }
        });

        return new CiselnikDetailViewModel
        {
            Key = key,
            Nazev = "Harmonogramové kroky",
            CanCreate = true,
            CanChangeLockState = canChangeLockState,
            SloupceNavic = new[] { "Barva" },
            IsHodnotaNavicSelect = false,
            IsHodnotaNavicRequired = true,
            Polozky = rows
        };
    }

    private int CountHarmonogramCatalogRows()
    {
        var schema = GetActiveHarmonogramSchema();
        return Math.Max(0, schema.Kroky.Count + 1);
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

    private int? ResolveProjectRoleId(string value)
        => _dbContext.CiselnikRoliProjektu
            .Where(x => x.Kod == value || x.Nazev == value)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();

    private int ResolveOsobaId(string value)
    {
        if (int.TryParse(value, out var id))
        {
            return id;
        }

        var normalized = _textNormalizer.Normalize(value);
        var osoby = _dbContext.Osoby.AsNoTracking().ToList();
        var exact = osoby.FirstOrDefault(x =>
            _personIdentityMatcher.NameEquals(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailEquals(x.Email, normalized));
        if (exact is not null)
        {
            return exact.Id;
        }

        var contains = osoby.FirstOrDefault(x =>
            _personIdentityMatcher.NameContains(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailContains(x.Email, normalized));
        if (contains is not null)
        {
            return contains.Id;
        }

        throw new InvalidOperationException($"Osoba '{value}' nebyla nalezena.");
    }

    private int GetNextCisloZaznamuTransactional(int projektId)
    {
        var existingValues = _dbContext.ProjektoveZaznamy
            .FromSqlRaw("SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE projekt_id = {0}", projektId)
            .Select(x => x.CisloZaznamu)
            .ToList();

        return RecordNumberAllocator.FindLowestAvailablePositive(existingValues);
    }

    private int AllocateMeetingOrderTransactional(int projektId, int cisloJednani)
    {
        var existingOrders = _dbContext.ProjektoveZaznamy
            .FromSqlRaw(
                "SELECT * FROM projektove_zaznamy WITH (UPDLOCK, HOLDLOCK) WHERE projekt_id = {0} AND cislo_viditelne_typ = {1} AND cislo_viditelne_a = {2}",
                projektId,
                RecordDisplayNumberTypeMeeting,
                cisloJednani)
            .Select(x => x.CisloViditelneB)
            .ToList();

        return RecordNumberAllocator.FindLowestAvailablePositive(existingOrders);
    }

    private void SaveHarmonogramStepRow(SaveCiselnikRowCommand command)
    {
        var rawColor = command.HodnotyNavic.FirstOrDefault()?.Trim();
        if (!IsValidHexColor(rawColor))
        {
            throw new InvalidOperationException("Barva kroku musí být ve formátu #RRGGBB.");
        }

        var colorHex = rawColor!.ToUpperInvariant();
        var clone = CloneActiveHarmonogramSchema();
        var rows = clone.ClonedRows.ToList();
        var durationRows = rows.Where(x => !x.JeZpozdeni).OrderBy(x => x.KrokPoradi).ThenBy(x => x.Id).ToList();

        if (command.Id.GetValueOrDefault() == HarmonogramDelayColorPseudoRowId)
        {
            clone.NewSchema.DelayBarvaHex = colorHex;
            FinalizeClonedHarmonogramSchema(clone.NewSchema, normalizeRows: false);
            return;
        }

        var kod = (command.Kod ?? string.Empty).Trim();
        var nazev = (command.Nazev ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(kod))
        {
            throw new InvalidOperationException("Kód kroku harmonogramu je povinný.");
        }

        if (string.IsNullOrWhiteSpace(nazev))
        {
            throw new InvalidOperationException("Název kroku harmonogramu je povinný.");
        }

        if (command.Id.HasValue)
        {
            if (!clone.ClonedBySourceId.TryGetValue(command.Id.Value, out var targetStep) || targetStep.JeZpozdeni)
            {
                throw new InvalidOperationException("Upravovat lze pouze hlavní kroky harmonogramu.");
            }

            var duplicateCodeExists = durationRows.Any(x => x.Id != targetStep.Id && Ci.Equals(x.Kod, kod));
            if (duplicateCodeExists)
            {
                throw new InvalidOperationException($"Krok s kódem '{kod}' už existuje.");
            }

            targetStep.Kod = kod;
            targetStep.Nazev = nazev;
            targetStep.BarvaHex = colorHex;

            var pairedDelay = rows.FirstOrDefault(x => x.JeZpozdeni && x.KrokKey == targetStep.KrokKey);
            if (pairedDelay is not null && string.IsNullOrWhiteSpace(pairedDelay.Nazev))
            {
                pairedDelay.Nazev = $"{nazev} - zpoždění";
            }
        }
        else
        {
            var duplicateCodeExists = durationRows.Any(x => Ci.Equals(x.Kod, kod));
            if (duplicateCodeExists)
            {
                throw new InvalidOperationException($"Krok s kódem '{kod}' už existuje.");
            }

            var nextOrder = durationRows.Count == 0 ? 1 : durationRows.Max(x => x.KrokPoradi) + 1;
            var stepKey = Guid.NewGuid();
            var usedCodes = rows.Select(x => x.Kod).Where(x => !string.IsNullOrWhiteSpace(x));
            var delayKod = BuildUniqueDelayCode($"{kod}_DELAY", usedCodes);

            _dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Kod = kod,
                Nazev = nazev,
                Hodnota = nextOrder,
                IsLocked = true,
                SablonaVerze = clone.NewSchema.Verze,
                KrokKey = stepKey,
                KrokPoradi = nextOrder,
                JeZpozdeni = false,
                BarvaHex = colorHex
            });

            _dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Kod = delayKod,
                Nazev = $"{nazev} - zpoždění",
                Hodnota = 100 + nextOrder,
                IsLocked = true,
                SablonaVerze = clone.NewSchema.Verze,
                KrokKey = stepKey,
                KrokPoradi = nextOrder,
                JeZpozdeni = true,
                BarvaHex = null
            });
        }

        _dbContext.SaveChanges();
        FinalizeClonedHarmonogramSchema(clone.NewSchema);
    }

    private void DeleteHarmonogramStepRow(DeleteCiselnikRowCommand command)
    {
        if (command.Id == HarmonogramDelayColorPseudoRowId)
        {
            throw new InvalidOperationException("Globální barvu zpoždění nelze smazat.");
        }

        var clone = CloneActiveHarmonogramSchema();
        if (!clone.ClonedBySourceId.TryGetValue(command.Id, out var targetStep) || targetStep.JeZpozdeni)
        {
            throw new InvalidOperationException("Smazat lze pouze hlavní kroky harmonogramu.");
        }

        var remainingMainSteps = clone.ClonedRows.Count(x => !x.JeZpozdeni && x.Id != targetStep.Id);
        if (remainingMainSteps <= 0)
        {
            throw new InvalidOperationException("Harmonogram musí obsahovat alespoň jeden krok.");
        }

        var pairedDelay = clone.ClonedRows.FirstOrDefault(x => x.JeZpozdeni && x.KrokKey == targetStep.KrokKey);
        _dbContext.CiselnikHarmonogramTypu.Remove(targetStep);
        if (pairedDelay is not null)
        {
            _dbContext.CiselnikHarmonogramTypu.Remove(pairedDelay);
        }

        _dbContext.SaveChanges();
        FinalizeClonedHarmonogramSchema(clone.NewSchema);
    }

    private HarmonogramSchemaCloneResult CloneActiveHarmonogramSchema()
    {
        var sourceSchema = _dbContext.HarmonogramSablony
            .OrderByDescending(x => x.IsAktivni)
            .ThenByDescending(x => x.Verze)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Aktivní harmonogramová šablona není dostupná.");

        var sourceRows = _dbContext.CiselnikHarmonogramTypu.AsNoTracking()
            .Where(x => x.SablonaVerze == sourceSchema.Verze)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.JeZpozdeni)
            .ThenBy(x => x.Id)
            .ToList();
        if (sourceRows.Count == 0)
        {
            throw new InvalidOperationException("Aktivní harmonogramová šablona neobsahuje žádné kroky.");
        }

        var newSchema = new HarmonogramSablonaEntity
        {
            DelayBarvaHex = NormalizeHexColor(sourceSchema.DelayBarvaHex, DefaultDelayBarvaHex),
            IsAktivni = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = null
        };
        _dbContext.HarmonogramSablony.Add(newSchema);
        _dbContext.SaveChanges();

        var mapBySourceId = new Dictionary<int, HarmonogramTypEntity>();
        var clonedRows = new List<HarmonogramTypEntity>(sourceRows.Count);
        foreach (var sourceRow in sourceRows)
        {
            var clone = new HarmonogramTypEntity
            {
                Kod = sourceRow.Kod,
                Nazev = sourceRow.Nazev,
                Hodnota = sourceRow.Hodnota,
                IsLocked = sourceRow.IsLocked,
                SablonaVerze = newSchema.Verze,
                KrokKey = sourceRow.KrokKey,
                KrokPoradi = sourceRow.KrokPoradi,
                JeZpozdeni = sourceRow.JeZpozdeni,
                BarvaHex = sourceRow.BarvaHex
            };
            _dbContext.CiselnikHarmonogramTypu.Add(clone);
            mapBySourceId[sourceRow.Id] = clone;
            clonedRows.Add(clone);
        }
        _dbContext.SaveChanges();

        return new HarmonogramSchemaCloneResult(sourceSchema, newSchema, mapBySourceId, clonedRows);
    }

    private void ActivateClonedHarmonogramSchema(HarmonogramSablonaEntity newSchema)
    {
        var activeSchemas = _dbContext.HarmonogramSablony
            .Where(x => x.IsAktivni && x.Verze != newSchema.Verze)
            .ToList();
        foreach (var schema in activeSchemas)
        {
            schema.IsAktivni = false;
        }

        newSchema.IsAktivni = true;
    }

    private void FinalizeClonedHarmonogramSchema(HarmonogramSablonaEntity newSchema, bool normalizeRows = true)
    {
        if (normalizeRows)
        {
            NormalizeHarmonogramSchemaRows(newSchema.Verze);
        }

        ActivateClonedHarmonogramSchema(newSchema);
    }

    private void NormalizeHarmonogramSchemaRows(int schemaVersion)
    {
        var rows = _dbContext.CiselnikHarmonogramTypu
            .Where(x => x.SablonaVerze == schemaVersion)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.JeZpozdeni)
            .ThenBy(x => x.Id)
            .ToList();

        var durationRows = rows
            .Where(x => !x.JeZpozdeni)
            .OrderBy(x => x.KrokPoradi)
            .ThenBy(x => x.Id)
            .ToList();

        var durationKeySet = durationRows.Select(x => x.KrokKey).ToHashSet();
        var orphanDelayRows = rows.Where(x => x.JeZpozdeni && !durationKeySet.Contains(x.KrokKey)).ToList();
        if (orphanDelayRows.Count > 0)
        {
            _dbContext.CiselnikHarmonogramTypu.RemoveRange(orphanDelayRows);
        }

        for (var index = 0; index < durationRows.Count; index += 1)
        {
            var order = index + 1;
            var duration = durationRows[index];
            duration.KrokPoradi = order;
            duration.Hodnota = order;
            duration.BarvaHex = NormalizeHexColor(duration.BarvaHex, ResolveDefaultStepColor(order));

            var delayRows = rows
                .Where(x => x.JeZpozdeni && x.KrokKey == duration.KrokKey)
                .OrderBy(x => x.Id)
                .ToList();
            foreach (var delay in delayRows)
            {
                delay.KrokPoradi = order;
                delay.Hodnota = 100 + order;
                delay.BarvaHex = null;
            }

            if (delayRows.Count == 0)
            {
                var fallbackDelayCode = BuildUniqueDelayCode($"{duration.Kod}_DELAY", rows.Select(x => x.Kod));
                _dbContext.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
                {
                    Kod = fallbackDelayCode,
                    Nazev = $"{duration.Nazev} - zpoždění",
                    Hodnota = 100 + order,
                    IsLocked = true,
                    SablonaVerze = schemaVersion,
                    KrokKey = duration.KrokKey,
                    KrokPoradi = order,
                    JeZpozdeni = true,
                    BarvaHex = null
                });
            }
        }
    }

    private static string BuildUniqueDelayCode(string baseCode, IEnumerable<string?> usedCodes)
    {
        var codeBase = string.IsNullOrWhiteSpace(baseCode) ? "STEP_DELAY" : baseCode.Trim();
        var used = new HashSet<string>(usedCodes.Where(x => !string.IsNullOrWhiteSpace(x))!.Select(x => x!.Trim()), Ci);
        if (!used.Contains(codeBase))
        {
            return codeBase;
        }

        var index = 2;
        while (used.Contains($"{codeBase}_{index}"))
        {
            index += 1;
        }

        return $"{codeBase}_{index}";
    }

    private void WriteAudit(int? actorOsobaId, string entityType, string entityId, string action, string? oldValue, string? newValue)
    {
        _dbContext.AuthzAuditLog.Add(new AuthzAuditLogEntity
        {
            ActorOsobaId = actorOsobaId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    private int ResolveAsProfileToOsobaId(string? asProfile)
    {
        if (string.IsNullOrWhiteSpace(asProfile))
        {
            var firstId = _dbContext.Osoby.AsNoTracking().OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefault();
            if (!firstId.HasValue)
            {
                throw new InvalidOperationException("Tabulka osoby je prázdná.");
            }

            return firstId.Value;
        }

        if (int.TryParse(asProfile, out var id))
        {
            return id;
        }

        if (Guid.TryParse(asProfile, out var guid))
        {
            var guidMatch = _dbContext.Osoby.AsNoTracking().Where(x => x.GuidAd == guid).Select(x => (int?)x.Id).FirstOrDefault();
            if (guidMatch.HasValue)
            {
                return guidMatch.Value;
            }
        }

        var normalized = _textNormalizer.Normalize(asProfile);
        var normalizedLoginCandidates = BuildNormalizedLoginCandidates(asProfile);
        var people = _dbContext.Osoby.AsNoTracking().ToList();
        var match = people.FirstOrDefault(x =>
            _personIdentityMatcher.NameEquals(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailEquals(x.Email, normalized) ||
            LoginEquals(x.AdLogin, normalizedLoginCandidates));
        if (match is not null)
        {
            return match.Id;
        }

        var containsMatch = people.FirstOrDefault(x =>
            _personIdentityMatcher.NameContains(x.Titul, x.Jmeno, x.Prijmeni, normalized) ||
            _personIdentityMatcher.EmailContains(x.Email, normalized) ||
            LoginContains(x.AdLogin, normalizedLoginCandidates));
        if (containsMatch is not null)
        {
            return containsMatch.Id;
        }

        throw new InvalidOperationException($"Uživatel '{asProfile}' nebyl nalezen.");
    }

    private string BuildDisplayName(string? titul, string jmeno, string prijmeni, int id)
    {
        var displayName = _personIdentityMatcher.BuildPersonName(titul, jmeno, prijmeni);
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
        var normalizedEmail = _textNormalizer.NormalizeEmail(email);
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

    private static string? NormalizeAdLogin(string? rawLogin)
    {
        if (string.IsNullOrWhiteSpace(rawLogin))
        {
            return null;
        }

        var value = rawLogin.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static HashSet<string> BuildNormalizedLoginCandidates(string? rawLogin)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawLogin))
        {
            return candidates;
        }

        void add(string? value)
        {
            var normalized = NormalizeAdLogin(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                candidates.Add(normalized);
            }
        }

        var trimmed = rawLogin.Trim();
        add(trimmed);

        if (trimmed.Contains('\\'))
        {
            var slashParts = trimmed.Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            add(slashParts.LastOrDefault());
        }

        if (trimmed.Contains('@'))
        {
            add(trimmed.Split('@', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault());
        }

        return candidates;
    }

    private static bool LoginEquals(string? storedLogin, IReadOnlyCollection<string> candidates)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(storedLogin))
        {
            return false;
        }

        return candidates.Contains(storedLogin.Trim().ToLowerInvariant());
    }

    private static bool LoginContains(string? storedLogin, IReadOnlyCollection<string> candidates)
    {
        if (candidates.Count == 0 || string.IsNullOrWhiteSpace(storedLogin))
        {
            return false;
        }

        var normalized = storedLogin.Trim().ToLowerInvariant();
        return candidates.Any(candidate => normalized.Contains(candidate, StringComparison.Ordinal));
    }

}
