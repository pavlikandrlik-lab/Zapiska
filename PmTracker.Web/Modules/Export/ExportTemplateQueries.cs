using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Modules.Export;

public sealed class ExportTemplateQueries : IExportTemplateQueries
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    private readonly PmTrackerDbContext _dbContext;
    private readonly ITextNormalizer _textNormalizer;
    private readonly IRichTextContentService _richTextContentService;
    private readonly IPersonIdentityMatcher _personIdentityMatcher;

    private sealed record ProjectRoleExportRow(int OsobaId, string Osoba, string RoleKod, string RoleNazev);
    private sealed record SubsystemRoleExportRow(int OsobaId, string Osoba, string RoleNazev, string SubsystemNazev);

    public ExportTemplateQueries(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IRichTextContentService richTextContentService,
        IPersonIdentityMatcher personIdentityMatcher)
    {
        _dbContext = dbContext;
        _textNormalizer = textNormalizer;
        _richTextContentService = richTextContentService;
        _personIdentityMatcher = personIdentityMatcher;
    }

    public ExportTemplateQueryResult GetProjectTemplate(int projektId)
    {
        var project = _dbContext.Projekty.AsNoTracking().FirstOrDefault(x => x.Id == projektId)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        return new ExportTemplateQueryResult
        {
            ExportVariant = "project_all",
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = null,
            JednaniCislo = null,
            JednaniDatum = null,
            JednaniMisto = null,
            JednaniStav = "Projekt",
            SnapshotSummary = "Tisk kompletního projektu bez filtru.",
            PreparationSummary = null,
            ProjektoveRole = BuildProjectExportRoleRows(project.Id),
            AppliedRuleSummary = ["Bez omezení"],
            Legenda = [],
            Zaznamy = BuildExportRecords(projektId, null, null, limitComments: false, applyMeetingSnapshotRules: false),
            Dochazka = []
        };
    }

    public ExportTemplateQueryResult GetMeetingTemplate(int jednaniId)
    {
        var meeting = _dbContext.Jednani.AsNoTracking().FirstOrDefault(x => x.Id == jednaniId)
            ?? throw new InvalidOperationException($"Jednání {jednaniId} nebylo nalezeno.");

        var project = _dbContext.Projekty.AsNoTracking().First(x => x.Id == meeting.ProjektId);
        var status = _dbContext.CiselnikStavuJednani.AsNoTracking().FirstOrDefault(x => x.Id == meeting.StavJednaniId);

        return new ExportTemplateQueryResult
        {
            ExportVariant = "meeting",
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = meeting.Id,
            JednaniCislo = meeting.CisloJednani,
            JednaniDatum = meeting.DatumPlanovane,
            JednaniMisto = meeting.Misto,
            JednaniStav = status?.Nazev ?? "-",
            SnapshotSummary = string.Empty,
            PreparationSummary = null,
            ProjektoveRole = BuildProjectExportRoleRows(project.Id),
            AppliedRuleSummary = ["Automatický meeting výstup"],
            Legenda = [],
            Zaznamy = BuildExportRecords(project.Id, meeting.Id, null, limitComments: true, applyMeetingSnapshotRules: true),
            Dochazka = BuildAttendanceGroups(meeting.Id, project.Id)
        };
    }

    public ExportTemplateQueryResult GetTaskTemplate(int projektId, int zaznamId)
    {
        var project = _dbContext.Projekty.AsNoTracking().FirstOrDefault(x => x.Id == projektId)
            ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

        var lastMeeting = _dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projektId)
            .OrderByDescending(x => x.CisloJednani)
            .FirstOrDefault();

        return new ExportTemplateQueryResult
        {
            ExportVariant = "task_single",
            ProjektId = project.Id,
            ProjektZkratka = project.Zkratka,
            ProjektNazev = project.CelyNazev,
            JednaniId = lastMeeting?.Id,
            JednaniCislo = lastMeeting?.CisloJednani,
            JednaniDatum = lastMeeting?.DatumPlanovane,
            JednaniMisto = lastMeeting?.Misto,
            JednaniStav = "Úkol",
            SnapshotSummary = "Tisk jednoho úkolu.",
            PreparationSummary = null,
            ProjektoveRole = [],
            AppliedRuleSummary = ["Automatický task výstup"],
            Legenda = [],
            Zaznamy = BuildExportRecords(projektId, lastMeeting?.Id, zaznamId, limitComments: true, applyMeetingSnapshotRules: false),
            Dochazka = []
        };
    }

    private List<PdfRoleAssignmentViewModel> BuildProjectExportRoleRows(int projectId)
    {
        var projectRoles = BuildActiveProjectRoleAssignments(projectId)
            .Select(item => new
            {
                item.Osoba,
                KindOrder = 0,
                item.RoleNazev,
                SubsystemNazev = (string?)null,
                ViewModel = new PdfRoleAssignmentViewModel
                {
                    Osoba = item.Osoba,
                    TypRole = "Projektová",
                    Role = item.RoleNazev,
                    Subsystem = null
                }
            });

        var subsystemRoles = BuildActiveProjectSubsystemRoleAssignments(projectId)
            .Select(item => new
            {
                item.Osoba,
                KindOrder = 1,
                item.RoleNazev,
                SubsystemNazev = (string?)item.SubsystemNazev,
                ViewModel = new PdfRoleAssignmentViewModel
                {
                    Osoba = item.Osoba,
                    TypRole = "Subsystémová",
                    Role = item.RoleNazev,
                    Subsystem = item.SubsystemNazev
                }
            });

        return projectRoles
            .Concat(subsystemRoles)
            .OrderBy(item => item.Osoba, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.KindOrder)
            .ThenBy(item => item.SubsystemNazev ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.RoleNazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => item.ViewModel)
            .ToList();
    }

    private List<ProjectRoleExportRow> BuildActiveProjectRoleAssignments(int projectId)
    {
        var assignments = _dbContext.ObsazeniProjektu.AsNoTracking()
            .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
            .ToList();
        var roles = _dbContext.CiselnikRoliProjektu.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var role = roles.GetValueOrDefault(assignment.RoleId);
                return new ProjectRoleExportRow(
                    assignment.OsobaId,
                    BuildDisplayNameFromOsoba(person),
                    role?.Kod ?? "-",
                    role?.Nazev ?? "-");
            })
            .ToList();
    }

    private List<SubsystemRoleExportRow> BuildActiveProjectSubsystemRoleAssignments(int projectId)
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
        var projectSubsystemById = projectSubsystems.ToDictionary(x => x.Id);

        return assignments
            .Select(assignment =>
            {
                var person = people.GetValueOrDefault(assignment.OsobaId);
                var projectSubsystem = projectSubsystemById.GetValueOrDefault(assignment.ProjektSubsystemId);
                var subsystem = projectSubsystem is null ? null : subsystems.GetValueOrDefault(projectSubsystem.SubsystemId);
                var role = roleById.GetValueOrDefault(assignment.RoleSubsystemuId);
                return new SubsystemRoleExportRow(
                    assignment.OsobaId,
                    BuildDisplayNameFromOsoba(person),
                    role?.Nazev ?? "-",
                    subsystem?.Nazev ?? "-");
            })
            .ToList();
    }

    private List<PdfAttendanceGroupViewModel> BuildAttendanceGroups(int meetingId, int projectId)
    {
        var attendanceRows = _dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .ToList();

        if (attendanceRows.Count == 0)
        {
            return BuildLegacyAttendanceGroups(projectId);
        }

        var attendanceStates = _dbContext.CiselnikStavuUcasti.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        return attendanceRows
            .GroupBy(x => x.StavUcastiId)
            .Select(group => new
            {
                State = attendanceStates.GetValueOrDefault(group.Key),
                Names = group
                    .Select(item => BuildDisplayNameFromOsoba(people.GetValueOrDefault(item.OsobaId)))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(Ci)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            })
            .Where(group => group.Names.Count > 0)
            .OrderBy(group => AttendancePrintOrder(group.State))
            .ThenBy(group => group.State?.Nazev ?? "Bez stavu", StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new PdfAttendanceGroupViewModel
            {
                Stav = group.State?.Nazev ?? "Bez stavu",
                Osoby = group.Names
            })
            .ToList();
    }

    private List<PdfAttendanceGroupViewModel> BuildLegacyAttendanceGroups(int projectId)
    {
        var participantNamesById = new Dictionary<int, string>();
        foreach (var assignment in BuildActiveProjectRoleAssignments(projectId))
        {
            if (!Ci.Equals(assignment.RoleKod, ProjectRoleCodes.Host))
            {
                participantNamesById[assignment.OsobaId] = assignment.Osoba;
            }
        }

        foreach (var assignment in BuildActiveProjectSubsystemRoleAssignments(projectId))
        {
            participantNamesById[assignment.OsobaId] = assignment.Osoba;
        }

        if (participantNamesById.Count == 0)
        {
            return [];
        }

        var defaultState = ResolveDefaultAttendanceState(_dbContext.CiselnikStavuUcasti.AsNoTracking().OrderBy(x => x.Id).ToList());

        return
        [
            new PdfAttendanceGroupViewModel
            {
                Stav = defaultState?.Nazev ?? "-",
                Osoby = participantNamesById.Values
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            }
        ];
    }

    private CiselnikStavuUcastiEntity? ResolveDefaultAttendanceState(IReadOnlyList<CiselnikStavuUcastiEntity> stateRows)
    {
        return stateRows.FirstOrDefault(x => Ci.Equals(x.Kod, "PRESENT"))
            ?? stateRows.FirstOrDefault(x =>
                _textNormalizer.Normalize(x.Nazev).Contains("pritomen", StringComparison.OrdinalIgnoreCase))
            ?? stateRows.FirstOrDefault();
    }

    private int AttendancePrintOrder(CiselnikStavuUcastiEntity? state)
    {
        if (state is null)
        {
            return 100;
        }

        if (Ci.Equals(state.Kod, "PRESENT"))
        {
            return 1;
        }

        if (Ci.Equals(state.Kod, "ONLINE"))
        {
            return 2;
        }

        if (Ci.Equals(state.Kod, "EXCUSED"))
        {
            return 3;
        }

        if (Ci.Equals(state.Kod, "MISSING") || Ci.Equals(state.Kod, "ABSENT"))
        {
            return 4;
        }

        var normalizedName = _textNormalizer.Normalize(state.Nazev);
        if (normalizedName.Contains("videokonference", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("online", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (normalizedName.Contains("neomluven", StringComparison.OrdinalIgnoreCase)
            || normalizedName.Contains("nepritomen", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (normalizedName.Contains("omluven", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (normalizedName.Contains("pritomen", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 100;
    }

    private List<PdfExportRecordViewModel> BuildExportRecords(
        int projectId,
        int? anchorMeetingId,
        int? specificRecordId,
        bool limitComments,
        bool applyMeetingSnapshotRules)
    {
        var records = _dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Where(x => !specificRecordId.HasValue || x.Id == specificRecordId.Value)
            .ToList();
        records = OrderRecordsByVisibleNumber(records).ToList();
        var recordIds = records.Select(record => record.Id).ToArray();

        var categories = _dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionary(x => x.Id);
        var taskTypes = _dbContext.CiselnikTypuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var taskStates = _dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionary(x => x.Id);
        var subsystems = _dbContext.Subsystemy.AsNoTracking().ToDictionary(x => x.Id);
        var people = _dbContext.Osoby.AsNoTracking().ToDictionary(x => x.Id);

        var comments = _dbContext.Vyjadreni.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToList();

        var meetings = _dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .OrderByDescending(x => x.CisloJednani)
            .ToList();

        var meetingStateMap = _dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionary(x => x.Id);
        var externalTypeMap = _dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().ToDictionary(x => x.Id);

        var externalLinksByRecord = _dbContext.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var collaboration = _dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var termHistory = _dbContext.ZaznamHistorieTerminu.AsNoTracking()
            .Where(x => recordIds.Contains(x.ZaznamId))
            .OrderByDescending(x => x.DatumZmeny)
            .ToList()
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var meetingById = meetings.ToDictionary(x => x.Id);
        var anchorMeeting = anchorMeetingId.HasValue ? meetings.FirstOrDefault(x => x.Id == anchorMeetingId.Value) : null;
        var anchorState = anchorMeeting is null ? null : meetingStateMap.GetValueOrDefault(anchorMeeting.StavJednaniId);
        var previousMeeting = anchorMeeting is null
            ? null
            : meetings.Where(x => x.CisloJednani < anchorMeeting.CisloJednani)
                .OrderByDescending(x => x.CisloJednani)
                .FirstOrDefault();
        var previousMeetingNumber = previousMeeting?.CisloJednani;

        if (applyMeetingSnapshotRules && anchorMeeting is not null)
        {
            var statusHistoryByRecord = _dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderByDescending(x => x.DatumZmeny)
                .ThenByDescending(x => x.Id)
                .ToList()
                .GroupBy(x => x.ZaznamId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var anchorMeetingDate = anchorMeeting.DatumPlanovane.Date;
            var previousMeetingDate = previousMeeting?.DatumPlanovane.Date;

            records = records
                .Where(record => IsRecordVisibleForMeetingPrint(
                    record,
                    taskStates,
                    statusHistoryByRecord.TryGetValue(record.Id, out var statusHistoryValues)
                        ? statusHistoryValues
                        : Array.Empty<ZaznamHistorieStavuZaznamuEntity>(),
                    anchorMeetingDate,
                    previousMeetingDate))
                .ToList();
        }

        var filteredComments = anchorMeeting is null
            ? comments
            : comments
                .Where(comment =>
                    meetingById.TryGetValue(comment.JednaniId, out var commentMeeting)
                    && commentMeeting.CisloJednani <= anchorMeeting.CisloJednani)
                .ToList();

        var commentGroups = filteredComments
            .GroupBy(x => x.ZaznamId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return records.Select(record =>
        {
            IReadOnlyList<VyjadreniEntity> commentsForRecord = commentGroups.TryGetValue(record.Id, out var groupedComments)
                ? groupedComments
                : [];
            var orderedCommentsForRecord = OrderCommentsForExport(commentsForRecord, meetingById);
            var selectedComments = limitComments
                ? ApplyCommentLimit(orderedCommentsForRecord)
                : orderedCommentsForRecord;

            var commentRows = selectedComments.Select(comment =>
            {
                meetingById.TryGetValue(comment.JednaniId, out var commentMeeting);
                var commentState = commentMeeting is null ? null : meetingStateMap.GetValueOrDefault(commentMeeting.StavJednaniId);
                var author = people.GetValueOrDefault(comment.AutorOsobaId) ?? people.GetValueOrDefault(record.VlastnikId);
                var highlightColor = ResolveHighlightColor(anchorMeeting, anchorState, previousMeetingNumber, commentMeeting);

                return new PdfExportCommentViewModel
                {
                    Autor = BuildDisplayNameFromOsoba(author),
                    Datum = comment.DatumVyjadreni,
                    Text = comment.TextVyjadreni,
                    Delka = _richTextContentService.ToPlainText(comment.TextVyjadreni).Length,
                    JednaniCislo = commentMeeting?.CisloJednani,
                    JednaniDatum = commentMeeting?.DatumPlanovane,
                    JednaniStav = commentState?.Nazev,
                    IsNew = !string.IsNullOrWhiteSpace(highlightColor),
                    HighlightColor = highlightColor
                };
            }).ToList();

            IReadOnlyList<ZaznamExterniOdkazEntity> externalRows = externalLinksByRecord.TryGetValue(record.Id, out var externalValues)
                ? externalValues
                : [];
            var external = externalRows
                .Select(link =>
                {
                    var typeCode = externalTypeMap.GetValueOrDefault(link.TypOdkazuId)?.Kod ?? "-";
                    return FormatExternalLinkDisplay(typeCode, link.Cislo, link.PredpokladanaCena, link.PlanDodani);
                })
                .ToList();

            IReadOnlyList<ZaznamSpolupraceEntity> collaborationRows = collaboration.TryGetValue(record.Id, out var collaborationValues)
                ? collaborationValues
                : [];
            var peopleCollab = collaborationRows
                .Select(item => BuildDisplayNameFromOsoba(people.GetValueOrDefault(item.OsobaId)))
                .Distinct(Ci)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            IReadOnlyList<ZaznamHistorieTerminuEntity> termHistoryRows = termHistory.TryGetValue(record.Id, out var termHistoryValues)
                ? termHistoryValues
                : [];
            var historyDates = termHistoryRows
                .Select(x => x.PuvodniDatum)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            var category = categories.GetValueOrDefault(record.KategorieId);
            var taskType = record.AktualniTypUkoluId.HasValue
                ? taskTypes.GetValueOrDefault(record.AktualniTypUkoluId.Value)
                : null;
            var subsystem = subsystems.GetValueOrDefault(record.SubsystemId);

            return new PdfExportRecordViewModel
            {
                ZaznamId = record.Id,
                CisloZaznamu = record.CisloZaznamu,
                CisloViditelne = ResolveVisibleRecordNumber(record),
                CisloViditelneA = ResolveVisibleNumberPartA(record),
                CisloViditelneB = ResolveVisibleNumberPartB(record),
                Nazev = record.Nazev,
                Cil = record.Cil,
                Popis = record.Popis,
                KategorieKod = category?.Kod ?? "-",
                Kategorie = category?.Nazev ?? "-",
                TypUkoluKod = taskType?.Kod,
                TypUkolu = taskType?.Nazev,
                Stav = record.StavUkoluId.HasValue ? taskStates.GetValueOrDefault(record.StavUkoluId.Value)?.Nazev ?? "-" : "-",
                Vlastnik = BuildDisplayNameFromOsoba(people.GetValueOrDefault(record.VlastnikId)),
                SubsystemKod = subsystem?.Kod ?? "-",
                Subsystem = subsystem?.Nazev ?? "-",
                DatumZalozeni = record.DatumZalozeni,
                HistorieTerminu = historyDates,
                Termin = record.DatumUkonceni,
                ExterniVazby = external,
                Spoluprace = peopleCollab,
                Vyjadreni = commentRows
            };
        }).ToList();
    }

    private static List<VyjadreniEntity> OrderCommentsForExport(
        IReadOnlyList<VyjadreniEntity> comments,
        IReadOnlyDictionary<int, JednaniEntity> meetingById)
    {
        return comments
            .OrderBy(comment => meetingById.GetValueOrDefault(comment.JednaniId)?.CisloJednani ?? 0)
            .ThenBy(comment => comment.Id)
            .ToList();
    }

    private static bool IsRecordVisibleForMeetingPrint(
        ProjektovyZaznamEntity record,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime anchorMeetingDate,
        DateTime? previousMeetingDate)
    {
        if (record.DatumZalozeni.Date > anchorMeetingDate.Date)
        {
            return false;
        }

        var statusAtAnchorMeeting = ResolveTaskStatusAtDate(record.StavUkoluId, statusHistory, anchorMeetingDate);
        if (!IsFinalTaskStatus(statusAtAnchorMeeting, taskStates))
        {
            return true;
        }

        if (!previousMeetingDate.HasValue)
        {
            return false;
        }

        var statusAtPreviousMeeting = ResolveTaskStatusAtDate(record.StavUkoluId, statusHistory, previousMeetingDate.Value);
        return !IsFinalTaskStatus(statusAtPreviousMeeting, taskStates);
    }

    private static int? ResolveTaskStatusAtDate(
        int? currentStatusId,
        IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
        DateTime targetDate)
    {
        var resolvedStatusId = currentStatusId;
        foreach (var change in statusHistory)
        {
            if (change.DatumZmeny.Date <= targetDate.Date)
            {
                continue;
            }

            if (resolvedStatusId.HasValue && resolvedStatusId.Value == change.NovyStav)
            {
                resolvedStatusId = change.PuvodniStav;
            }
        }

        return resolvedStatusId;
    }

    private static bool IsFinalTaskStatus(
        int? statusId,
        IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates)
    {
        return statusId.HasValue
            && taskStates.GetValueOrDefault(statusId.Value)?.IsFinal == true;
    }

    private List<VyjadreniEntity> ApplyCommentLimit(IReadOnlyList<VyjadreniEntity> comments)
    {
        const int lineBudgetPerTask = 20;
        const int estimatedCharsPerLine = 95;

        var selected = new List<VyjadreniEntity>();
        var usedLines = 0;

        for (var index = comments.Count - 1; index >= 0; index--)
        {
            var comment = comments[index];
            var commentPlainText = _richTextContentService.ToPlainText(comment.TextVyjadreni);
            var estimatedTextLines = EstimateCommentTextLines(commentPlainText, estimatedCharsPerLine);
            var estimatedLines = 1 + estimatedTextLines;

            if (selected.Count > 0 && usedLines + estimatedLines > lineBudgetPerTask)
            {
                break;
            }

            selected.Add(comment);
            usedLines += estimatedLines;
        }

        selected.Reverse();
        return selected;
    }

    private static int EstimateCommentTextLines(string plainText, int estimatedCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return 1;
        }

        var normalized = plainText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var estimatedLines = 0;
        var rows = normalized.Split('\n');
        foreach (var row in rows)
        {
            estimatedLines += Math.Max(1, (int)Math.Ceiling(row.Length / (double)estimatedCharsPerLine));
        }

        return Math.Max(1, estimatedLines);
    }

    private static string? ResolveHighlightColor(
        JednaniEntity? anchorMeeting,
        CiselnikStavuJednaniEntity? anchorState,
        int? previousMeetingNumber,
        JednaniEntity? commentMeeting)
    {
        if (anchorMeeting is null || commentMeeting is null || anchorState is null)
        {
            return null;
        }

        var anchorIsPreparation = anchorState.Kod.Equals("DRAFT", StringComparison.OrdinalIgnoreCase)
            || anchorState.Nazev.Contains("příprav", StringComparison.OrdinalIgnoreCase);

        if (!anchorIsPreparation)
        {
            return commentMeeting.Id == anchorMeeting.Id ? "#0F4D8A" : null;
        }

        if (commentMeeting.Id == anchorMeeting.Id)
        {
            return "#A63A2B";
        }

        if (previousMeetingNumber.HasValue && commentMeeting.CisloJednani == previousMeetingNumber.Value)
        {
            return "#0F4D8A";
        }

        return null;
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
        return record.CisloViditelneTyp == 1
            ? Math.Max(1, record.CisloViditelneB)
            : 0;
    }

    private static IEnumerable<ProjektovyZaznamEntity> OrderRecordsByVisibleNumber(IEnumerable<ProjektovyZaznamEntity> rows)
    {
        return rows
            .OrderBy(ResolveVisibleNumberPartA)
            .ThenBy(ResolveVisibleNumberPartB)
            .ThenBy(x => x.CisloZaznamu);
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

    private static string FormatEstimatedPrice(decimal estimatedPrice)
    {
        return $"{estimatedPrice.ToString("N2", CultureInfo.GetCultureInfo("cs-CZ"))} Kč";
    }

    private static string FormatExternalLinkDisplay(string typeCode, string number, decimal? estimatedPrice, DateTime? plannedDelivery)
    {
        var header = $"{typeCode} {number}".Trim();
        var details = new List<string>();
        if (estimatedPrice.HasValue)
        {
            details.Add(FormatEstimatedPrice(estimatedPrice.Value));
        }

        if (plannedDelivery.HasValue)
        {
            details.Add($"plán dodání: {plannedDelivery.Value:dd.MM.yyyy}");
        }

        return details.Count > 0
            ? $"{header} ({string.Join(", ", details)})"
            : header;
    }
}
