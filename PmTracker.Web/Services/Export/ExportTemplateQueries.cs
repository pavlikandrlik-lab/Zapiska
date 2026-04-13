using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Web.Services.Export
{
    using PmTracker.Web.Services.Export.Queries;

    public interface IExportTemplateQueries
    {
        Task<ExportTemplateQueryResult> GetProjectTemplateAsync(int projektId, CurrentUserContextViewModel currentUser, ProjectExportRecordFilters? filters = null, CancellationToken ct = default);
        Task<ExportTemplateQueryResult> GetMeetingTemplateAsync(int jednaniId, CancellationToken ct = default);
        Task<ExportTemplateQueryResult> GetTaskTemplateAsync(int projektId, int zaznamId, CancellationToken ct = default);
    }

    public sealed record class ExportTemplateQueryResult
    {
        public required string ExportVariant { get; init; }
        public int ProjektId { get; init; }
        public string? ProjektZkratka { get; init; }
        public required string ProjektNazev { get; init; }
        public int? JednaniId { get; init; }
        public int? JednaniCislo { get; init; }
        public DateTime? JednaniDatum { get; init; }
        public string? JednaniMisto { get; init; }
        public required string JednaniStav { get; init; }
        public required string SnapshotSummary { get; init; }
        public string? PreparationSummary { get; init; }
        public IReadOnlyList<PdfAttendanceGroupViewModel> Dochazka { get; init; } = [];
        public IReadOnlyList<PdfRoleAssignmentViewModel> ProjektoveRole { get; init; } = [];
        public required IReadOnlyList<string> AppliedRuleSummary { get; init; }
        public required IReadOnlyList<PdfLegendItemViewModel> Legenda { get; init; }
        public required IReadOnlyList<PdfExportRecordViewModel> Zaznamy { get; init; }
    }

    public sealed class ExportTemplateQueries : IExportTemplateQueries
    {
        private readonly PmTrackerDbContext dbContext;
        private readonly IExportRoleProjectionBuilder exportRoleProjectionBuilder;
        private readonly IExportAttendanceProjectionBuilder exportAttendanceProjectionBuilder;
        private readonly IExportRecordProjectionBuilder exportRecordProjectionBuilder;
        private readonly IExportTemplateSummaryBuilder exportTemplateSummaryBuilder;

        public ExportTemplateQueries(
            PmTrackerDbContext dbContext,
            IExportRoleProjectionBuilder exportRoleProjectionBuilder,
            IExportAttendanceProjectionBuilder exportAttendanceProjectionBuilder,
            IExportRecordProjectionBuilder exportRecordProjectionBuilder,
            IExportTemplateSummaryBuilder exportTemplateSummaryBuilder)
        {
            this.dbContext = dbContext;
            this.exportRoleProjectionBuilder = exportRoleProjectionBuilder;
            this.exportAttendanceProjectionBuilder = exportAttendanceProjectionBuilder;
            this.exportRecordProjectionBuilder = exportRecordProjectionBuilder;
            this.exportTemplateSummaryBuilder = exportTemplateSummaryBuilder;
        }

        public async Task<ExportTemplateQueryResult> GetProjectTemplateAsync(int projektId, CurrentUserContextViewModel currentUser, ProjectExportRecordFilters? filters = null, CancellationToken ct = default)
        {
            var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projektId, ct)
                ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");
            var normalizedFilters = NormalizeProjectExportFilters(filters);
            var appliedRuleSummary = normalizedFilters is { UseCurrentFilters: true, HasRelevantFilters: true }
                ? await BuildProjectFilterSummaryAsync(normalizedFilters, currentUser.OsobaId, ct)
                : Array.Empty<string>();
            var summary = normalizedFilters is { UseCurrentFilters: true, HasRelevantFilters: true }
                ? exportTemplateSummaryBuilder.BuildFilteredProjectSummary(appliedRuleSummary)
                : exportTemplateSummaryBuilder.BuildProjectSummary();

            return new ExportTemplateQueryResult
            {
                ExportVariant = normalizedFilters is { UseCurrentFilters: true, HasRelevantFilters: true } ? "project_filtered" : "project_all",
                ProjektId = project.Id,
                ProjektZkratka = project.Zkratka,
                ProjektNazev = project.CelyNazev,
                JednaniId = null,
                JednaniCislo = null,
                JednaniDatum = null,
                JednaniMisto = null,
                JednaniStav = summary.JednaniStav,
                SnapshotSummary = summary.SnapshotSummary,
                PreparationSummary = summary.PreparationSummary,
                ProjektoveRole = await exportRoleProjectionBuilder.BuildProjectRoleRowsAsync(project.Id, ct),
                AppliedRuleSummary = summary.AppliedRuleSummary,
                Legenda = summary.Legenda,
                Zaznamy = await exportRecordProjectionBuilder.BuildExportRecordsAsync(projektId, null, null, limitComments: false, applyMeetingSnapshotRules: false, normalizedFilters, currentUser.OsobaId, ct),
                Dochazka = []
            };
        }

        public async Task<ExportTemplateQueryResult> GetMeetingTemplateAsync(int jednaniId, CancellationToken ct = default)
        {
            var meeting = await dbContext.Jednani.AsNoTracking().FirstOrDefaultAsync(x => x.Id == jednaniId, ct)
                ?? throw new InvalidOperationException($"Jednání {jednaniId} nebylo nalezeno.");

            var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meeting.ProjektId, ct)
                ?? throw new InvalidOperationException($"Projekt {meeting.ProjektId} nebyl nalezen.");
            var status = await dbContext.CiselnikStavuJednani.AsNoTracking().FirstOrDefaultAsync(x => x.Id == meeting.StavJednaniId, ct);
            var summary = exportTemplateSummaryBuilder.BuildMeetingSummary(status?.Nazev);

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
                JednaniStav = summary.JednaniStav,
                SnapshotSummary = summary.SnapshotSummary,
                PreparationSummary = summary.PreparationSummary,
                ProjektoveRole = await exportRoleProjectionBuilder.BuildProjectRoleRowsAsync(project.Id, ct),
                AppliedRuleSummary = summary.AppliedRuleSummary,
                Legenda = summary.Legenda,
                Zaznamy = await exportRecordProjectionBuilder.BuildExportRecordsAsync(project.Id, meeting.Id, null, limitComments: true, applyMeetingSnapshotRules: true, null, null, ct),
                Dochazka = await exportAttendanceProjectionBuilder.BuildAttendanceGroupsAsync(meeting.Id, project.Id, ct)
            };
        }

        public async Task<ExportTemplateQueryResult> GetTaskTemplateAsync(int projektId, int zaznamId, CancellationToken ct = default)
        {
            var project = await dbContext.Projekty.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projektId, ct)
                ?? throw new InvalidOperationException($"Projekt {projektId} nebyl nalezen.");

            var lastMeeting = await dbContext.Jednani.AsNoTracking()
                .Where(x => x.ProjektId == projektId)
                .OrderByDescending(x => x.CisloJednani)
                .FirstOrDefaultAsync(ct);
            var summary = exportTemplateSummaryBuilder.BuildTaskSummary();

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
                JednaniStav = summary.JednaniStav,
                SnapshotSummary = summary.SnapshotSummary,
                PreparationSummary = summary.PreparationSummary,
                ProjektoveRole = [],
                AppliedRuleSummary = summary.AppliedRuleSummary,
                Legenda = summary.Legenda,
                Zaznamy = await exportRecordProjectionBuilder.BuildExportRecordsAsync(projektId, lastMeeting?.Id, zaznamId, limitComments: true, applyMeetingSnapshotRules: false, null, null, ct),
                Dochazka = []
            };
        }

        private static ProjectExportRecordFilters? NormalizeProjectExportFilters(ProjectExportRecordFilters? filters)
        {
            if (filters is null)
            {
                return null;
            }

            return filters with
            {
                Subsystem = string.IsNullOrWhiteSpace(filters.Subsystem) ? null : filters.Subsystem.Trim(),
                Kategorie = string.IsNullOrWhiteSpace(filters.Kategorie) ? null : filters.Kategorie.Trim(),
                Stav = string.IsNullOrWhiteSpace(filters.Stav) ? null : filters.Stav.Trim(),
                Typ = string.IsNullOrWhiteSpace(filters.Typ) ? null : filters.Typ.Trim(),
                VlastnikId = filters.VlastnikId > 0 ? filters.VlastnikId : null,
                JednaniVyjadreniStavId = filters.JednaniVyjadreniStavId > 0 ? filters.JednaniVyjadreniStavId : null
            };
        }

        private async Task<IReadOnlyList<string>> BuildProjectFilterSummaryAsync(ProjectExportRecordFilters filters, int currentUserOsobaId, CancellationToken ct)
        {
            var appliedRules = new List<string>();

            if (!string.IsNullOrWhiteSpace(filters.Subsystem))
            {
                var subsystemRow = await dbContext.Subsystemy.AsNoTracking()
                    .Where(x => x.Kod == filters.Subsystem || (string.IsNullOrEmpty(x.Kod) && x.Nazev == filters.Subsystem))
                    .Select(x => new { x.Kod, x.Nazev })
                    .FirstOrDefaultAsync(ct);
                var subsystemLabel = subsystemRow is null
                    ? null
                    : string.IsNullOrWhiteSpace(subsystemRow.Kod)
                        ? subsystemRow.Nazev
                        : $"{subsystemRow.Kod} - {subsystemRow.Nazev}";
                appliedRules.Add($"Subsystém: {(string.IsNullOrWhiteSpace(subsystemLabel) ? filters.Subsystem : subsystemLabel)}");
            }

            if (!string.IsNullOrWhiteSpace(filters.Kategorie))
            {
                var categoryLabel = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
                    .Where(x => x.Kod == filters.Kategorie)
                    .Select(x => x.Nazev)
                    .FirstOrDefaultAsync(ct);
                appliedRules.Add($"Kategorie: {(string.IsNullOrWhiteSpace(categoryLabel) ? filters.Kategorie : categoryLabel)}");
            }

            if (!string.IsNullOrWhiteSpace(filters.Stav))
            {
                var taskStateLabel = await dbContext.CiselnikStavuUkolu.AsNoTracking()
                    .Where(x => x.Kod == filters.Stav)
                    .Select(x => x.Nazev)
                    .FirstOrDefaultAsync(ct);
                appliedRules.Add($"Stav úkolu: {(string.IsNullOrWhiteSpace(taskStateLabel) ? filters.Stav : taskStateLabel)}");
            }

            if (!string.IsNullOrWhiteSpace(filters.Typ))
            {
                var taskTypeLabel = await dbContext.CiselnikTypuUkolu.AsNoTracking()
                    .Where(x => x.Kod == filters.Typ)
                    .Select(x => x.Nazev)
                    .FirstOrDefaultAsync(ct);
                appliedRules.Add($"Typ úkolu: {(string.IsNullOrWhiteSpace(taskTypeLabel) ? filters.Typ : taskTypeLabel)}");
            }

            if (filters.VlastnikId.HasValue)
            {
                var ownerRow = await dbContext.Osoby.AsNoTracking()
                    .Where(x => x.Id == filters.VlastnikId.Value)
                    .Select(x => new { x.Titul, x.Jmeno, x.Prijmeni })
                    .FirstOrDefaultAsync(ct);
                var ownerLabel = ownerRow is null
                    ? null
                    : string.Join(" ", new[] { ownerRow.Titul, ownerRow.Jmeno, ownerRow.Prijmeni }.Where(value => !string.IsNullOrWhiteSpace(value)));
                appliedRules.Add($"Vlastník: {(string.IsNullOrWhiteSpace(ownerLabel) ? $"Osoba #{filters.VlastnikId.Value}" : ownerLabel)}");
            }

            if (filters.Aktivni)
            {
                appliedRules.Add("Pouze aktivní úkoly");
            }

            if (filters.Mine)
            {
                var mineLabel = currentUserOsobaId > 0
                    ? "Jen mé záznamy"
                    : "Jen mé záznamy (bez identifikované osoby)";
                appliedRules.Add(mineLabel);
            }

            if (filters.JednaniVyjadreniStavId.HasValue)
            {
                var meetingStateLabel = await dbContext.CiselnikStavuJednani.AsNoTracking()
                    .Where(x => x.Id == filters.JednaniVyjadreniStavId.Value)
                    .Select(x => x.Nazev)
                    .FirstOrDefaultAsync(ct);
                appliedRules.Add($"Jednání-vyjádření: {(string.IsNullOrWhiteSpace(meetingStateLabel) ? filters.JednaniVyjadreniStavId.Value.ToString(CultureInfo.InvariantCulture) : meetingStateLabel)}");
            }

            return appliedRules;
        }
    }
}

namespace PmTracker.Web.Services.Export.Queries
{
    public interface IExportAttendanceProjectionBuilder
    {
        Task<IReadOnlyList<PdfAttendanceGroupViewModel>> BuildAttendanceGroupsAsync(int meetingId, int projectId, CancellationToken ct = default);
    }

    public interface IExportCommentProjectionBuilder
    {
        List<VyjadreniEntity> OrderForExport(IReadOnlyList<VyjadreniEntity> comments, IReadOnlyDictionary<int, JednaniEntity> meetingById);
        List<VyjadreniEntity> ApplyLimit(IReadOnlyList<VyjadreniEntity> comments);
    }

    public interface IExportRecordProjectionBuilder
    {
        Task<IReadOnlyList<PdfExportRecordViewModel>> BuildExportRecordsAsync(
            int projectId,
            int? anchorMeetingId,
            int? specificRecordId,
            bool limitComments,
            bool applyMeetingSnapshotRules,
            ProjectExportRecordFilters? projectFilters,
            int? currentUserOsobaId,
            CancellationToken ct = default);
    }

    public interface IExportRecordVisibilityEvaluator
    {
        bool IsVisibleForMeetingPrint(
            ProjektovyZaznamEntity record,
            IReadOnlyDictionary<int, CiselnikStavuUkoluEntity> taskStates,
            IReadOnlyList<ZaznamHistorieStavuZaznamuEntity> statusHistory,
            DateTime anchorMeetingDate,
            DateTime? previousMeetingDate);
    }

    public interface IExportRoleProjectionBuilder
    {
        Task<IReadOnlyList<PdfRoleAssignmentViewModel>> BuildProjectRoleRowsAsync(int projectId, CancellationToken ct = default);
    }

    public interface IExportTemplateSummaryBuilder
    {
        ExportTemplateSummaryProjection BuildProjectSummary();
        ExportTemplateSummaryProjection BuildFilteredProjectSummary(IReadOnlyList<string> appliedRuleSummary);
        ExportTemplateSummaryProjection BuildMeetingSummary(string? meetingStatusName);
        ExportTemplateSummaryProjection BuildTaskSummary();
    }

    public sealed record class ExportTemplateSummaryProjection
    {
        public required string JednaniStav { get; init; }
        public required string SnapshotSummary { get; init; }
        public string? PreparationSummary { get; init; }
        public required IReadOnlyList<string> AppliedRuleSummary { get; init; }
        public required IReadOnlyList<PdfLegendItemViewModel> Legenda { get; init; }
    }

    public sealed class ExportTemplateSummaryBuilder : IExportTemplateSummaryBuilder
    {
        public ExportTemplateSummaryProjection BuildProjectSummary()
        {
            return new ExportTemplateSummaryProjection
            {
                JednaniStav = "Projekt",
                SnapshotSummary = "Tisk kompletního projektu bez filtru.",
                PreparationSummary = null,
                AppliedRuleSummary = ["Bez omezení"],
                Legenda = []
            };
        }

        public ExportTemplateSummaryProjection BuildFilteredProjectSummary(IReadOnlyList<string> appliedRuleSummary)
        {
            return new ExportTemplateSummaryProjection
            {
                JednaniStav = "Projekt",
                SnapshotSummary = "Tisk projektu s použitím aktivních filtrů.",
                PreparationSummary = null,
                AppliedRuleSummary = appliedRuleSummary.Count > 0 ? appliedRuleSummary : ["Aktivní projektové filtry"],
                Legenda = []
            };
        }

        public ExportTemplateSummaryProjection BuildMeetingSummary(string? meetingStatusName)
        {
            return new ExportTemplateSummaryProjection
            {
                JednaniStav = string.IsNullOrWhiteSpace(meetingStatusName) ? "-" : meetingStatusName.Trim(),
                SnapshotSummary = string.Empty,
                PreparationSummary = null,
                AppliedRuleSummary = ["Automatický meeting výstup"],
                Legenda = []
            };
        }

        public ExportTemplateSummaryProjection BuildTaskSummary()
        {
            return new ExportTemplateSummaryProjection
            {
                JednaniStav = "Úkol",
                SnapshotSummary = "Tisk jednoho úkolu.",
                PreparationSummary = null,
                AppliedRuleSummary = ["Automatický task výstup"],
                Legenda = []
            };
        }
    }

    public sealed class ExportCommentProjectionBuilder(IRichTextContentService richTextContentService) : IExportCommentProjectionBuilder
    {
        public List<VyjadreniEntity> OrderForExport(IReadOnlyList<VyjadreniEntity> comments, IReadOnlyDictionary<int, JednaniEntity> meetingById)
        {
            return comments
                .OrderBy(comment => meetingById.GetValueOrDefault(comment.JednaniId)?.CisloJednani ?? 0)
                .ThenBy(comment => comment.Id)
                .ToList();
        }

        public List<VyjadreniEntity> ApplyLimit(IReadOnlyList<VyjadreniEntity> comments)
        {
            const int lineBudgetPerTask = 20;
            const int estimatedCharsPerLine = 95;

            var selected = new List<VyjadreniEntity>();
            var usedLines = 0;

            for (var index = comments.Count - 1; index >= 0; index--)
            {
                var comment = comments[index];
                var commentPlainText = richTextContentService.ToPlainText(comment.TextVyjadreni);
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
    }

    public sealed class ExportRecordVisibilityEvaluator : IExportRecordVisibilityEvaluator
    {
        public bool IsVisibleForMeetingPrint(
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
    }

    public sealed class ExportRoleProjectionBuilder(
        PmTrackerDbContext dbContext,
        IPersonIdentityMatcher personIdentityMatcher) : IExportRoleProjectionBuilder
    {
        private sealed record ProjectRoleExportRow(int OsobaId, string Osoba, string RoleKod, string RoleNazev);
        private sealed record SubsystemRoleExportRow(int OsobaId, string Osoba, string RoleNazev, string SubsystemNazev);

        public async Task<IReadOnlyList<PdfRoleAssignmentViewModel>> BuildProjectRoleRowsAsync(int projectId, CancellationToken ct = default)
        {
            var projectRoles = (await BuildActiveProjectRoleAssignmentsAsync(projectId, ct))
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

            var subsystemRoles = (await BuildActiveProjectSubsystemRoleAssignmentsAsync(projectId, ct))
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

        private async Task<List<ProjectRoleExportRow>> BuildActiveProjectRoleAssignmentsAsync(int projectId, CancellationToken ct)
        {
            var assignments = await dbContext.ObsazeniProjektu.AsNoTracking()
                .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
                .ToListAsync(ct);
            var roles = await dbContext.CiselnikRoliProjektu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
            var personIds = assignments.Select(x => x.OsobaId).Distinct().ToArray();
            var people = personIds.Length == 0
                ? new Dictionary<int, OsobaEntity>()
                : await dbContext.Osoby.AsNoTracking()
                    .Where(x => personIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

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

        private async Task<List<SubsystemRoleExportRow>> BuildActiveProjectSubsystemRoleAssignmentsAsync(int projectId, CancellationToken ct)
        {
            var projectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
                .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
                .ToListAsync(ct);
            var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
            var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && !x.DatumOdebrani.HasValue)
                .ToListAsync(ct);
            var personIds = assignments.Select(x => x.OsobaId).Distinct().ToArray();
            var people = personIds.Length == 0
                ? new Dictionary<int, OsobaEntity>()
                : await dbContext.Osoby.AsNoTracking()
                    .Where(x => personIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);
            var subsystemIds = projectSubsystems.Select(x => x.SubsystemId).Distinct().ToArray();
            var subsystems = subsystemIds.Length == 0
                ? new Dictionary<int, SubsystemEntity>()
                : await dbContext.Subsystemy.AsNoTracking()
                    .Where(x => subsystemIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);
            var roleById = await dbContext.CiselnikRoliSubsystemu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
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

        private string BuildDisplayNameFromOsoba(OsobaEntity? osoba)
        {
            if (osoba is null)
            {
                return "-";
            }

            var displayName = personIdentityMatcher.BuildPersonName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni);
            return string.IsNullOrWhiteSpace(displayName)
                ? $"Uživatel #{osoba.Id}"
                : displayName;
        }
    }

    public sealed class ExportAttendanceProjectionBuilder(
        PmTrackerDbContext dbContext,
        ITextNormalizer textNormalizer,
        IPersonIdentityMatcher personIdentityMatcher) : IExportAttendanceProjectionBuilder
    {
        private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

        private sealed record ProjectRoleExportRow(int OsobaId, string Osoba, string RoleKod);
        private sealed record SubsystemRoleExportRow(int OsobaId, string Osoba);

        public async Task<IReadOnlyList<PdfAttendanceGroupViewModel>> BuildAttendanceGroupsAsync(int meetingId, int projectId, CancellationToken ct = default)
        {
            var attendanceRows = await dbContext.Ucast.AsNoTracking()
                .Where(x => x.JednaniId == meetingId)
                .ToListAsync(ct);

            if (attendanceRows.Count == 0)
            {
                return await BuildLegacyAttendanceGroupsAsync(projectId, ct);
            }

            var attendanceStates = await dbContext.CiselnikStavuUcasti.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
            var participantIds = attendanceRows
                .Select(x => x.OsobaId)
                .Distinct()
                .ToArray();
            var people = participantIds.Length == 0
                ? new Dictionary<int, OsobaEntity>()
                : await dbContext.Osoby.AsNoTracking()
                    .Where(x => participantIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

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

        private async Task<IReadOnlyList<PdfAttendanceGroupViewModel>> BuildLegacyAttendanceGroupsAsync(int projectId, CancellationToken ct)
        {
            var participantNamesById = new Dictionary<int, string>();
            foreach (var assignment in await BuildActiveProjectRoleAssignmentsAsync(projectId, ct))
            {
                if (!Ci.Equals(assignment.RoleKod, ProjectRoleCodes.Host))
                {
                    participantNamesById[assignment.OsobaId] = assignment.Osoba;
                }
            }

            foreach (var assignment in await BuildActiveProjectSubsystemRoleAssignmentsAsync(projectId, ct))
            {
                participantNamesById[assignment.OsobaId] = assignment.Osoba;
            }

            if (participantNamesById.Count == 0)
            {
                return [];
            }

            var defaultState = ResolveDefaultAttendanceState(await dbContext.CiselnikStavuUcasti.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));

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

        private async Task<List<ProjectRoleExportRow>> BuildActiveProjectRoleAssignmentsAsync(int projectId, CancellationToken ct)
        {
            var assignments = await dbContext.ObsazeniProjektu.AsNoTracking()
                .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
                .ToListAsync(ct);
            var roles = await dbContext.CiselnikRoliProjektu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
            var personIds = assignments.Select(x => x.OsobaId).Distinct().ToArray();
            var people = personIds.Length == 0
                ? new Dictionary<int, OsobaEntity>()
                : await dbContext.Osoby.AsNoTracking()
                    .Where(x => personIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

            return assignments
                .Select(assignment =>
                {
                    var person = people.GetValueOrDefault(assignment.OsobaId);
                    var role = roles.GetValueOrDefault(assignment.RoleId);
                    return new ProjectRoleExportRow(
                        assignment.OsobaId,
                        BuildDisplayNameFromOsoba(person),
                        role?.Kod ?? "-");
                })
                .ToList();
        }

        private async Task<List<SubsystemRoleExportRow>> BuildActiveProjectSubsystemRoleAssignmentsAsync(int projectId, CancellationToken ct)
        {
            var projectSubsystems = await dbContext.ProjektSubsystemy.AsNoTracking()
                .Where(x => x.ProjektId == projectId && !x.DatumOdebrani.HasValue)
                .ToListAsync(ct);
            var projectSubsystemIds = projectSubsystems.Select(x => x.Id).ToHashSet();
            var assignments = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                .Where(x => projectSubsystemIds.Contains(x.ProjektSubsystemId) && !x.DatumOdebrani.HasValue)
                .ToListAsync(ct);
            var personIds = assignments.Select(x => x.OsobaId).Distinct().ToArray();
            var people = personIds.Length == 0
                ? new Dictionary<int, OsobaEntity>()
                : await dbContext.Osoby.AsNoTracking()
                    .Where(x => personIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

            return assignments
                .Select(assignment => new SubsystemRoleExportRow(
                    assignment.OsobaId,
                    BuildDisplayNameFromOsoba(people.GetValueOrDefault(assignment.OsobaId))))
                .ToList();
        }

        private CiselnikStavuUcastiEntity? ResolveDefaultAttendanceState(IReadOnlyList<CiselnikStavuUcastiEntity> stateRows)
        {
            return stateRows.FirstOrDefault(x => Ci.Equals(x.Kod, "PRESENT"))
                ?? stateRows.FirstOrDefault(x =>
                    textNormalizer.Normalize(x.Nazev).Contains("pritomen", StringComparison.OrdinalIgnoreCase))
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

            var normalizedName = textNormalizer.Normalize(state.Nazev);
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

        private string BuildDisplayNameFromOsoba(OsobaEntity? osoba)
        {
            if (osoba is null)
            {
                return "-";
            }

            var displayName = personIdentityMatcher.BuildPersonName(osoba.Titul, osoba.Jmeno, osoba.Prijmeni);
            return string.IsNullOrWhiteSpace(displayName)
                ? $"Uživatel #{osoba.Id}"
                : displayName;
        }
    }

    public sealed class ExportRecordProjectionBuilder(
        PmTrackerDbContext dbContext,
        IRichTextContentService richTextContentService,
        IPersonIdentityMatcher personIdentityMatcher,
        IExportCommentProjectionBuilder exportCommentProjectionBuilder,
        IExportRecordVisibilityEvaluator exportRecordVisibilityEvaluator) : IExportRecordProjectionBuilder
    {
        private const string NewInformationHighlightColor = "#2563EB";
        private const string PreparationHighlightColor = "#DC2626";
        private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

        public async Task<IReadOnlyList<PdfExportRecordViewModel>> BuildExportRecordsAsync(
            int projectId,
            int? anchorMeetingId,
            int? specificRecordId,
            bool limitComments,
            bool applyMeetingSnapshotRules,
            ProjectExportRecordFilters? projectFilters,
            int? currentUserOsobaId,
            CancellationToken ct = default)
        {
            var recordsQuery = dbContext.ProjektoveZaznamy.AsNoTracking()
                .Where(x => x.ProjektId == projectId)
                .Where(x => !specificRecordId.HasValue || x.Id == specificRecordId.Value);

            if (projectFilters is { UseCurrentFilters: true, HasRelevantFilters: true })
            {
                if (!string.IsNullOrWhiteSpace(projectFilters.Subsystem))
                {
                    var matchingSubsystemIds = await dbContext.Subsystemy.AsNoTracking()
                        .Where(x => x.Kod == projectFilters.Subsystem || (string.IsNullOrEmpty(x.Kod) && x.Nazev == projectFilters.Subsystem))
                        .Select(x => x.Id)
                        .ToArrayAsync(ct);
                    if (matchingSubsystemIds.Length == 0)
                    {
                        return [];
                    }

                    recordsQuery = recordsQuery.Where(x => matchingSubsystemIds.Contains(x.SubsystemId));
                }

                if (!string.IsNullOrWhiteSpace(projectFilters.Kategorie))
                {
                    var categoryIds = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
                        .Where(x => x.Kod == projectFilters.Kategorie)
                        .Select(x => x.Id)
                        .ToArrayAsync(ct);
                    if (categoryIds.Length == 0)
                    {
                        return [];
                    }

                    recordsQuery = recordsQuery.Where(x => categoryIds.Contains(x.KategorieId));
                }

                if (!string.IsNullOrWhiteSpace(projectFilters.Stav))
                {
                    var taskStateIds = await dbContext.CiselnikStavuUkolu.AsNoTracking()
                        .Where(x => x.Kod == projectFilters.Stav)
                        .Select(x => x.Id)
                        .ToArrayAsync(ct);
                    if (taskStateIds.Length == 0)
                    {
                        return [];
                    }

                    recordsQuery = recordsQuery.Where(x => x.StavUkoluId.HasValue && taskStateIds.Contains(x.StavUkoluId.Value));
                }

                if (!string.IsNullOrWhiteSpace(projectFilters.Typ))
                {
                    var taskTypeIds = await dbContext.CiselnikTypuUkolu.AsNoTracking()
                        .Where(x => x.Kod == projectFilters.Typ)
                        .Select(x => x.Id)
                        .ToArrayAsync(ct);
                    if (taskTypeIds.Length == 0)
                    {
                        return [];
                    }

                    recordsQuery = recordsQuery.Where(x => x.AktualniTypUkoluId.HasValue && taskTypeIds.Contains(x.AktualniTypUkoluId.Value));
                }

                if (projectFilters.VlastnikId.HasValue)
                {
                    recordsQuery = recordsQuery.Where(x => x.VlastnikId == projectFilters.VlastnikId.Value);
                }

                if (projectFilters.Aktivni)
                {
                    var activeTaskStateIds = await dbContext.CiselnikStavuUkolu.AsNoTracking()
                        .Where(x => !x.IsFinal)
                        .Select(x => x.Id)
                        .ToArrayAsync(ct);
                    recordsQuery = recordsQuery.Where(x => !x.StavUkoluId.HasValue || activeTaskStateIds.Contains(x.StavUkoluId.Value));
                }

                if (projectFilters.Mine)
                {
                    if (!currentUserOsobaId.HasValue || currentUserOsobaId.Value <= 0)
                    {
                        return [];
                    }

                    recordsQuery = recordsQuery.Where(x => x.VlastnikId == currentUserOsobaId.Value);
                }

                if (projectFilters.JednaniVyjadreniStavId.HasValue)
                {
                    var taskCategoryIds = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
                        .Where(x => x.Kod == "U" || x.Kod == "UKOL" || x.Nazev.Contains("úkol") || x.Nazev.Contains("ukol"))
                        .Select(x => x.Id)
                        .ToArrayAsync(ct);
                    if (taskCategoryIds.Length == 0)
                    {
                        return [];
                    }

                    var matchingRecordIds = await (
                            from comment in dbContext.Vyjadreni.AsNoTracking()
                            join meeting in dbContext.Jednani.AsNoTracking() on comment.JednaniId equals meeting.Id
                            where meeting.StavJednaniId == projectFilters.JednaniVyjadreniStavId.Value
                            select comment.ZaznamId)
                        .Distinct()
                        .ToArrayAsync(ct);
                    if (matchingRecordIds.Length == 0)
                    {
                        return [];
                    }

                    recordsQuery = recordsQuery.Where(x => taskCategoryIds.Contains(x.KategorieId) && matchingRecordIds.Contains(x.Id));
                }
            }

            var records = await recordsQuery.ToListAsync(ct);
            records = [.. OrderRecordsByVisibleNumber(records)];
            var recordIds = records.Select(record => record.Id).ToArray();

            var categories = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
            var taskTypes = await dbContext.CiselnikTypuUkolu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
            var taskStates = await dbContext.CiselnikStavuUkolu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
            var subsystemIds = records.Select(x => x.SubsystemId).Distinct().ToArray();
            var subsystems = subsystemIds.Length == 0
                ? new Dictionary<int, SubsystemEntity>()
                : await dbContext.Subsystemy.AsNoTracking()
                    .Where(x => subsystemIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);
            var subsystemOrderById = subsystemIds.Length == 0
                ? new Dictionary<int, int>()
                : await ProjectSubsystemOrderingQuery.LoadActiveOrderBySubsystemIdAsync(dbContext, projectId, subsystemIds, ct);

            var comments = await dbContext.Vyjadreni.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .ToListAsync(ct);

            var meetings = await dbContext.Jednani.AsNoTracking()
                .Where(x => x.ProjektId == projectId)
                .OrderByDescending(x => x.CisloJednani)
                .ToListAsync(ct);

            var meetingStateMap = await dbContext.CiselnikStavuJednani.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
            var externalTypeMap = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking().ToDictionaryAsync(x => x.Id, ct);

            var externalLinksByRecord = (await dbContext.ZaznamExterniOdkazy.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .ToListAsync(ct))
                .GroupBy(x => x.ZaznamId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var collaboration = (await dbContext.ZaznamSpoluprace.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .ToListAsync(ct))
                .GroupBy(x => x.ZaznamId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var termHistory = (await dbContext.ZaznamHistorieTerminu.AsNoTracking()
                .Where(x => recordIds.Contains(x.ZaznamId))
                .OrderByDescending(x => x.DatumZmeny)
                .ToListAsync(ct))
                .GroupBy(x => x.ZaznamId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var peopleIds = records
                .Select(record => record.VlastnikId)
                .Concat(comments.Select(comment => comment.AutorOsobaId))
                .Concat(collaboration.Values.SelectMany(rows => rows.Select(item => item.OsobaId)))
                .Distinct()
                .ToArray();
            var people = peopleIds.Length == 0
                ? new Dictionary<int, OsobaEntity>()
                : await dbContext.Osoby.AsNoTracking()
                    .Where(x => peopleIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);

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
                var statusHistoryByRecord = (await dbContext.ZaznamHistorieStavuZaznamu.AsNoTracking()
                    .Where(x => recordIds.Contains(x.ZaznamId))
                    .OrderByDescending(x => x.DatumZmeny)
                    .ThenByDescending(x => x.Id)
                    .ToListAsync(ct))
                    .GroupBy(x => x.ZaznamId)
                    .ToDictionary(group => group.Key, group => group.ToList());

                var anchorMeetingDate = anchorMeeting.DatumPlanovane.Date;
                var previousMeetingDate = previousMeeting?.DatumPlanovane.Date;

                records = records
                    .Where(record => exportRecordVisibilityEvaluator.IsVisibleForMeetingPrint(
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
                var orderedCommentsForRecord = exportCommentProjectionBuilder.OrderForExport(commentsForRecord, meetingById);
                var selectedComments = limitComments
                    ? exportCommentProjectionBuilder.ApplyLimit(orderedCommentsForRecord)
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
                        Delka = richTextContentService.ToPlainText(comment.TextVyjadreni).Length,
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
                var hasProjectOrder = subsystemOrderById.TryGetValue(record.SubsystemId, out var subsystemOrder);

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
                    IsPaused = record.StavUkoluId.HasValue
                        && (taskStates.GetValueOrDefault(record.StavUkoluId.Value)?.Nazev ?? string.Empty)
                            .Contains("pozastav", StringComparison.CurrentCultureIgnoreCase),
                    Vlastnik = BuildDisplayNameFromOsoba(people.GetValueOrDefault(record.VlastnikId)),
                    SubsystemKod = subsystem?.Kod ?? "-",
                    Subsystem = subsystem?.Nazev ?? "-",
                    SubsystemPoradi = hasProjectOrder ? subsystemOrder : 0,
                    SubsystemHasProjectOrder = hasProjectOrder,
                    DatumZalozeni = record.DatumZalozeni,
                    HistorieTerminu = historyDates,
                    Termin = record.DatumUkonceni,
                    ExterniVazby = external,
                    Spoluprace = peopleCollab,
                    Vyjadreni = commentRows
                };
            }).ToList();
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
                return commentMeeting.Id == anchorMeeting.Id ? NewInformationHighlightColor : null;
            }

            if (commentMeeting.Id == anchorMeeting.Id)
            {
                return PreparationHighlightColor;
            }

            if (previousMeetingNumber.HasValue && commentMeeting.CisloJednani == previousMeetingNumber.Value)
            {
                return NewInformationHighlightColor;
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
}
