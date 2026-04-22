using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Records;

namespace PmTracker.Web.Services.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private const int NewsBatchSize = 200;
    private const string LegacyRecordEntityType = "projektove_zaznamy";
    private readonly PmTrackerDbContext _dbContext;
    private readonly IDashboardPriorityQuery _dashboardPriorityQuery;
    private readonly TimeProvider _timeProvider;

    public DashboardService(
        PmTrackerDbContext dbContext,
        IDashboardPriorityQuery dashboardPriorityQuery,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _dashboardPriorityQuery = dashboardPriorityQuery;
        _timeProvider = timeProvider;
    }

    public DashboardPageViewModel BuildDashboardPage(CurrentUserContextViewModel currentUser)
    {
        return new DashboardPageViewModel
        {
            CurrentUserContext = currentUser,
            PageTitle = "Přehled",
            Subtitle = "Osobní rozcestník přes projekty, jednání a relevantní změny.",
            FocusPanelUrl = "/dashboard/focus-panel",
            MeetingsPanelUrl = "/dashboard/meetings-panel",
            NewsPanelUrl = "/dashboard/news-panel"
        };
    }

    public async Task<DashboardFocusPanelViewModel> BuildFocusPanelAsync(CurrentUserContextViewModel currentUser, int limit, CancellationToken ct)
    {
        var totalCount = await _dashboardPriorityQuery.CountForUserAsync(currentUser.OsobaId, ct);
        var priorityItems = await _dashboardPriorityQuery.GetTopForUserAsync(currentUser.OsobaId, Math.Max(limit, 0), ct);
        var items = await BuildFocusItemDetailsAsync(priorityItems.Select(item => item.RecordId).ToList(), ct);
        return new DashboardFocusPanelViewModel
        {
            TotalCount = totalCount,
            ListUrl = "/dashboard/focus",
            Items = items
        };
    }

    public async Task<DashboardMeetingsPanelViewModel> BuildMeetingsPanelAsync(CurrentUserContextViewModel currentUser, int limit, CancellationToken ct)
    {
        var allItems = await BuildMeetingItemsAsync(currentUser, ct);
        return new DashboardMeetingsPanelViewModel
        {
            TotalCount = allItems.Count,
            ListUrl = "/dashboard/meetings",
            Items = allItems.Take(Math.Max(limit, 0)).ToList()
        };
    }

    public async Task<DashboardNewsPanelViewModel> BuildNewsPanelAsync(CurrentUserContextViewModel currentUser, int take, CancellationToken ct)
    {
        var normalizedTake = Math.Max(take, 0);
        var allItems = await BuildNewsItemsAsync(currentUser, ct);
        var items = allItems.Take(normalizedTake).ToList();
        return new DashboardNewsPanelViewModel
        {
            LoadedCount = items.Count,
            TotalCount = allItems.Count,
            CanLoadMore = items.Count < allItems.Count,
            LoadMoreUrl = items.Count < allItems.Count ? $"/dashboard/news-panel?take={normalizedTake + 5}" : null,
            ListUrl = "/dashboard/news",
            Items = items
        };
    }

    public async Task<DashboardFocusListPageViewModel> BuildFocusListPageAsync(CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        var priorityItems = await _dashboardPriorityQuery.GetAllForUserAsync(currentUser.OsobaId, ct);
        return new DashboardFocusListPageViewModel
        {
            CurrentUserContext = currentUser,
            PageTitle = "Na co se soustředit",
            Subtitle = "Relevantní neukončené záznamy přes všechny vaše projekty.",
            BackUrl = "/dashboard",
            BackLabel = "Zpět na přehled",
            Items = await BuildFocusItemDetailsAsync(priorityItems.Select(item => item.RecordId).ToList(), ct)
        };
    }

    public async Task<DashboardMeetingsListPageViewModel> BuildMeetingsListPageAsync(CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        return new DashboardMeetingsListPageViewModel
        {
            CurrentUserContext = currentUser,
            PageTitle = "Nejbližší jednání",
            Subtitle = "Budoucí jednání v projektech, kde jste aktivně zapojený.",
            BackUrl = "/dashboard",
            BackLabel = "Zpět na přehled",
            Items = await BuildMeetingItemsAsync(currentUser, ct)
        };
    }

    public async Task<DashboardNewsListPageViewModel> BuildNewsListPageAsync(CurrentUserContextViewModel currentUser, int take, CancellationToken ct)
    {
        var normalizedTake = Math.Max(take, 0);
        var allItems = await BuildNewsItemsAsync(currentUser, ct);
        var items = allItems.Take(normalizedTake).ToList();
        return new DashboardNewsListPageViewModel
        {
            CurrentUserContext = currentUser,
            PageTitle = "Co je nového",
            Subtitle = "Poslední relevantní změny od ostatních uživatelů.",
            BackUrl = "/dashboard",
            BackLabel = "Zpět na přehled",
            LoadedCount = items.Count,
            TotalCount = allItems.Count,
            CanLoadMore = items.Count < allItems.Count,
            LoadMoreUrl = items.Count < allItems.Count ? $"/dashboard/news?take={normalizedTake + 20}" : null,
            Items = items
        };
    }

    private async Task<List<DashboardFocusItemViewModel>> BuildFocusItemDetailsAsync(IReadOnlyList<int> recordIds, CancellationToken ct)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        var detailRows = await (
                from record in _dbContext.ProjektoveZaznamy.AsNoTracking()
                join project in _dbContext.Projekty.AsNoTracking() on record.ProjektId equals project.Id
                join owner in _dbContext.Osoby.AsNoTracking() on record.VlastnikId equals owner.Id
                join subsystem in _dbContext.Subsystemy.AsNoTracking() on record.SubsystemId equals subsystem.Id
                join state in _dbContext.CiselnikStavuUkolu.AsNoTracking() on record.StavUkoluId equals state.Id into stateGroup
                from state in stateGroup.DefaultIfEmpty()
                where recordIds.Contains(record.Id)
                select new FocusRecordDetailRow(
                    record.Id,
                    record.ProjektId,
                    project.Zkratka,
                    project.CelyNazev,
                    record.CisloViditelne ?? string.Empty,
                    record.Nazev,
                    record.Cil,
                    record.VlastnikId,
                    BuildPersonDisplayName(owner.Jmeno, owner.Prijmeni),
                    subsystem.Nazev,
                    state != null ? state.Nazev : "Bez stavu",
                    record.DatumZalozeni,
                    record.DatumUkonceni))
            .ToListAsync(ct);

        var detailRowsById = detailRows.ToDictionary(row => row.RecordId);

        return recordIds
            .Where(detailRowsById.ContainsKey)
            .Select(recordId => detailRowsById[recordId])
            .Select(row => new DashboardFocusItemViewModel
            {
                RecordId = row.RecordId,
                ProjectId = row.ProjectId,
                ProjectCode = row.ProjectCode,
                ProjectName = row.ProjectName,
                RecordNumber = row.RecordNumber,
                Title = row.Title,
                Goal = row.Goal,
                Owner = row.Owner,
                Subsystem = row.Subsystem,
                State = row.State,
                CreatedAt = row.CreatedAt,
                Deadline = row.Deadline
            })
            .ToList();
    }

    private async Task<HashSet<int>> BuildRelevantRecordIdsForDashboardNewsAsync(CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        var userOsobaId = currentUser.OsobaId;
        var accessibleProjectIds = BuildAccessibleProjectIds(currentUser);
        var hasGlobalProjectRead = HasGlobalProjectReadAccess(currentUser);

        var collaborationRecordIds = await _dbContext.ZaznamSpoluprace.AsNoTracking()
            .Where(item => item.OsobaId == userOsobaId)
            .Select(item => item.ZaznamId)
            .Distinct()
            .ToListAsync(ct);
        var collaborationSet = collaborationRecordIds.ToHashSet();

        var leadAssignments = await (
                from assignment in _dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
                join role in _dbContext.CiselnikRoliSubsystemu.AsNoTracking() on assignment.RoleSubsystemuId equals role.Id
                join projectSubsystem in _dbContext.ProjektSubsystemy.AsNoTracking() on assignment.ProjektSubsystemId equals projectSubsystem.Id
                where assignment.OsobaId == userOsobaId
                    && !assignment.DatumOdebrani.HasValue
                    && !projectSubsystem.DatumOdebrani.HasValue
                    && (role.Kod == SubsystemRoleCodes.Lead || role.Kod == SubsystemRoleCodes.DeputyLead)
                select new
                {
                    projectSubsystem.ProjektId,
                    projectSubsystem.SubsystemId
                })
            .Distinct()
            .ToListAsync(ct);
        var leadSubsystemSet = leadAssignments
            .Select(item => $"{item.ProjektId}:{item.SubsystemId}")
            .ToHashSet(StringComparer.Ordinal);

        var candidateRows = await (
                from record in _dbContext.ProjektoveZaznamy.AsNoTracking()
                join state in _dbContext.CiselnikStavuUkolu.AsNoTracking() on record.StavUkoluId equals state.Id into stateGroup
                from state in stateGroup.DefaultIfEmpty()
                where (!record.StavUkoluId.HasValue || !(state != null && state.IsFinal))
                    && (hasGlobalProjectRead || accessibleProjectIds.Contains(record.ProjektId))
                select new RelevantRecordRow(
                    record.Id,
                    record.ProjektId,
                    record.VlastnikId,
                    record.SubsystemId))
            .ToListAsync(ct);

        return candidateRows
            .Where(row =>
                row.OwnerId == userOsobaId
                || collaborationSet.Contains(row.RecordId)
                || leadSubsystemSet.Contains($"{row.ProjectId}:{row.SubsystemId}"))
            .Select(row => row.RecordId)
            .ToHashSet();
    }

    private async Task<List<DashboardMeetingItemViewModel>> BuildMeetingItemsAsync(CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        var accessibleProjectIds = BuildAccessibleProjectIds(currentUser);
        var hasGlobalProjectRead = HasGlobalProjectReadAccess(currentUser);
        var today = _timeProvider.GetLocalNow().Date;

        return await (
                from meeting in _dbContext.Jednani.AsNoTracking()
                join project in _dbContext.Projekty.AsNoTracking() on meeting.ProjektId equals project.Id
                join state in _dbContext.CiselnikStavuJednani.AsNoTracking() on meeting.StavJednaniId equals state.Id
                where meeting.DatumPlanovane >= today
                    && (hasGlobalProjectRead || accessibleProjectIds.Contains(meeting.ProjektId))
                orderby meeting.DatumPlanovane, meeting.CasZacatek, meeting.CisloJednani descending
                select new DashboardMeetingItemViewModel
                {
                    MeetingId = meeting.Id,
                    ProjectId = meeting.ProjektId,
                    ProjectCode = project.Zkratka,
                    ProjectName = project.CelyNazev,
                    Meeting = new JednaniListItemViewModel
                    {
                        Id = meeting.Id,
                        CisloJednani = meeting.CisloJednani,
                        Datum = meeting.DatumPlanovane,
                        CasZacatek = meeting.CasZacatek,
                        Misto = meeting.Misto ?? "-",
                        StavKod = state.Kod,
                        Stav = state.Nazev,
                        UzamklOsoba = null
                    }
                })
            .ToListAsync(ct);
    }

    private async Task<List<DashboardNewsItemViewModel>> BuildNewsItemsAsync(CurrentUserContextViewModel currentUser, CancellationToken ct)
    {
        var userOsobaId = currentUser.OsobaId;
        var accessibleProjectIds = BuildAccessibleProjectIds(currentUser);
        var hasGlobalProjectRead = HasGlobalProjectReadAccess(currentUser);
        var relevantRecordIds = await BuildRelevantRecordIdsForDashboardNewsAsync(currentUser, ct);

        var items = new List<DashboardNewsItemViewModel>();
        var skip = 0;

        while (true)
        {
            var auditRows = await _dbContext.AuthzAuditLog.AsNoTracking()
                .Where(item =>
                    item.ActorOsobaId.HasValue
                    && item.ActorOsobaId.Value != userOsobaId
                    && (item.EntityType == AuditEntityType.Comment.ToDatabaseValue()
                        || item.EntityType == AuditEntityType.Record.ToDatabaseValue()
                        || item.EntityType == LegacyRecordEntityType
                        || item.EntityType == AuditEntityType.Meeting.ToDatabaseValue())
                    && (item.Action == AuditActionType.Create.ToDatabaseValue()
                        || item.Action == AuditActionType.Update.ToDatabaseValue()))
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Skip(skip)
                .Take(NewsBatchSize)
                .Select(item => new AuditRow(
                    item.Id,
                    item.ActorOsobaId,
                    item.EntityType,
                    item.EntityId,
                    item.Action,
                    item.CreatedAt))
                .ToListAsync(ct);

            if (auditRows.Count == 0)
            {
                break;
            }

            skip += auditRows.Count;

            // Úprava #5 (2026-04-20): ActorName resolve z OsobaId → celé jméno (pro popis
            // "Pavel Andrlík • [kontext]"). Batch lookup podle ActorOsobaId v auditu.
            var actorIds = auditRows
                .Where(r => r.ActorOsobaId.HasValue)
                .Select(r => r.ActorOsobaId!.Value)
                .Distinct()
                .ToArray();

            var actorNameMap = actorIds.Length == 0
                ? new Dictionary<int, string>()
                : await _dbContext.Osoby
                    .Where(o => actorIds.Contains(o.Id))
                    .Select(o => new { o.Id, FullName = (o.Jmeno + " " + o.Prijmeni).Trim() })
                    .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

            string ResolveActorName(int? actorId) =>
                actorId.HasValue && actorNameMap.TryGetValue(actorId.Value, out var name) ? name : string.Empty;

            var commentIds = auditRows
                .Where(item => item.EntityType == AuditEntityType.Comment.ToDatabaseValue())
                .Select(item => ParseIntId(item.EntityId))
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .Distinct()
                .ToList();

            var recordIds = auditRows
                .Where(item => item.EntityType == AuditEntityType.Record.ToDatabaseValue() || item.EntityType == LegacyRecordEntityType)
                .Select(item => ParseIntId(item.EntityId))
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .Distinct()
                .ToList();

            var meetingIds = auditRows
                .Where(item => item.EntityType == AuditEntityType.Meeting.ToDatabaseValue())
                .Select(item => ParseIntId(item.EntityId))
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .Distinct()
                .ToList();

            var commentsById = commentIds.Count == 0
                ? new Dictionary<int, CommentAuditRow>()
                : (await (
                        from comment in _dbContext.Vyjadreni.AsNoTracking()
                        where commentIds.Contains(comment.Id)
                        join record in _dbContext.ProjektoveZaznamy.AsNoTracking() on comment.ZaznamId equals record.Id
                        join project in _dbContext.Projekty.AsNoTracking() on record.ProjektId equals project.Id
                        select new CommentAuditRow(
                            comment.Id,
                            comment.ZaznamId,
                            record.ProjektId,
                            project.Zkratka,
                            project.CelyNazev,
                            record.CisloViditelne ?? string.Empty,
                            record.Nazev,
                            comment.TextVyjadreni))
                    .ToDictionaryAsync(item => item.CommentId, ct));

            var recordsById = recordIds.Count == 0
                ? new Dictionary<int, RecordAuditRow>()
                : (await (
                        from record in _dbContext.ProjektoveZaznamy.AsNoTracking()
                        where recordIds.Contains(record.Id)
                        join project in _dbContext.Projekty.AsNoTracking() on record.ProjektId equals project.Id
                        join category in _dbContext.CiselnikKategoriiZaznamu.AsNoTracking() on record.KategorieId equals category.Id
                        select new RecordAuditRow(
                            record.Id,
                            record.ProjektId,
                            project.Zkratka,
                            project.CelyNazev,
                            record.CisloViditelne ?? string.Empty,
                            record.Nazev,
                            category.Kod ?? string.Empty,
                            category.Nazev ?? string.Empty,
                            record.Cil))
                    .ToDictionaryAsync(item => item.RecordId, ct));

            var meetingsById = meetingIds.Count == 0
                ? new Dictionary<int, MeetingAuditRow>()
                : (await (
                        from meeting in _dbContext.Jednani.AsNoTracking()
                        where meetingIds.Contains(meeting.Id)
                        join project in _dbContext.Projekty.AsNoTracking() on meeting.ProjektId equals project.Id
                        select new MeetingAuditRow(
                            meeting.Id,
                            meeting.ProjektId,
                            project.Zkratka,
                            project.CelyNazev,
                            meeting.CisloJednani,
                            meeting.DatumPlanovane,
                            meeting.CasZacatek))
                    .ToDictionaryAsync(item => item.MeetingId, ct));

            foreach (var row in auditRows)
            {
                if (string.Equals(row.EntityType, AuditEntityType.Comment.ToDatabaseValue(), StringComparison.Ordinal))
                {
                    var commentId = ParseIntId(row.EntityId);
                    if (!commentId.HasValue || !commentsById.TryGetValue(commentId.Value, out var comment))
                    {
                        continue;
                    }

                    if (!relevantRecordIds.Contains(comment.RecordId))
                    {
                        continue;
                    }

                    var commentActorName = ResolveActorName(row.ActorOsobaId);
                    var commentContext = TruncateFirstLine(comment.CommentText, 120);
                    items.Add(new DashboardNewsItemViewModel
                    {
                        AuditLogId = row.Id,
                        ProjectId = comment.ProjectId,
                        RecordId = comment.RecordId,
                        EntityType = row.EntityType,
                        Action = row.Action,
                        EventLabel = string.Equals(row.Action, AuditActionType.Create.ToDatabaseValue(), StringComparison.OrdinalIgnoreCase) ? "Nové vyjádření" : "Upravené vyjádření",
                        ActorName = commentActorName,
                        Title = $"{comment.RecordNumber} - {comment.RecordTitle}",
                        Description = commentContext,
                        ProjectLabel = BuildProjectLabel(comment.ProjectCode, comment.ProjectName),
                        RecordNumber = comment.RecordNumber,
                        CreatedAt = row.CreatedAt,
                        OpensComments = true
                    });
                    continue;
                }

                if (string.Equals(row.EntityType, AuditEntityType.Record.ToDatabaseValue(), StringComparison.Ordinal)
                    || string.Equals(row.EntityType, LegacyRecordEntityType, StringComparison.Ordinal))
                {
                    var recordId = ParseIntId(row.EntityId);
                    if (!recordId.HasValue || !recordsById.TryGetValue(recordId.Value, out var record))
                    {
                        continue;
                    }

                    if (!(hasGlobalProjectRead || accessibleProjectIds.Contains(record.ProjectId)))
                    {
                        continue;
                    }

                    var isCreate = string.Equals(row.Action, AuditActionType.Create.ToDatabaseValue(), StringComparison.OrdinalIgnoreCase);
                    var isTask = RecordCategoryClassifier.IsTaskCategory(record.CategoryCode, record.CategoryName);
                    var isInfoOrDecision = RecordCategoryClassifier.IsInformationOrDecisionCategory(record.CategoryCode, record.CategoryName);

                    if (isTask)
                    {
                        if (!relevantRecordIds.Contains(record.RecordId))
                        {
                            continue;
                        }
                    }
                    else if (isInfoOrDecision)
                    {
                        if (!isCreate)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    var eventLabel = isTask
                        ? (isCreate ? "Nový úkol" : "Změna úkolu")
                        : (string.IsNullOrWhiteSpace(record.CategoryName) ? "Nový záznam" : $"Nové: {record.CategoryName}");

                    var recordActorName = ResolveActorName(row.ActorOsobaId);
                    var recordContext = TruncateFirstLine(record.Cil, 120);
                    items.Add(new DashboardNewsItemViewModel
                    {
                        AuditLogId = row.Id,
                        ProjectId = record.ProjectId,
                        RecordId = record.RecordId,
                        EntityType = row.EntityType,
                        Action = row.Action,
                        EventLabel = eventLabel,
                        ActorName = recordActorName,
                        Title = $"{record.RecordNumber} - {record.RecordTitle}",
                        Description = recordContext,
                        ProjectLabel = BuildProjectLabel(record.ProjectCode, record.ProjectName),
                        RecordNumber = record.RecordNumber,
                        CreatedAt = row.CreatedAt,
                        OpensComments = false
                    });
                    continue;
                }

                var meetingId = ParseIntId(row.EntityId);
                if (!meetingId.HasValue || !meetingsById.TryGetValue(meetingId.Value, out var meeting))
                {
                    continue;
                }

                if (!(hasGlobalProjectRead || accessibleProjectIds.Contains(meeting.ProjectId)))
                {
                    continue;
                }

                items.Add(new DashboardNewsItemViewModel
                {
                    AuditLogId = row.Id,
                    ProjectId = meeting.ProjectId,
                    MeetingId = meeting.MeetingId,
                    EntityType = row.EntityType,
                    Action = row.Action,
                    EventLabel = string.Equals(row.Action, AuditActionType.Create.ToDatabaseValue(), StringComparison.OrdinalIgnoreCase) ? "Nové jednání" : "Změna jednání",
                    ActorName = ResolveActorName(row.ActorOsobaId),
                    Title = $"Jednání č. {meeting.MeetingNumber}",
                    Description = $"{meeting.Date:dd.MM.yyyy} v {meeting.StartTime:HH\\:mm}",
                    ProjectLabel = BuildProjectLabel(meeting.ProjectCode, meeting.ProjectName),
                    CreatedAt = row.CreatedAt,
                    OpensComments = false
                });
            }
        }

        return items
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.AuditLogId)
            .ToList();
    }

    private static string BuildPersonDisplayName(string firstName, string lastName)
    {
        return string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static string BuildProjectLabel(string code, string name)
    {
        return string.IsNullOrWhiteSpace(code)
            ? name
            : $"{code} - {name}";
    }

    private static int? ParseIntId(string? entityId)
    {
        return int.TryParse(entityId, out var value) ? value : null;
    }

    private static string TruncateFirstLine(string? source, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }
        var firstLine = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', 2)[0]
            .Trim();
        if (firstLine.Length <= maxLen)
        {
            return firstLine;
        }
        return firstLine.Substring(0, Math.Max(0, maxLen - 1)).TrimEnd() + "…";
    }

    private static bool HasGlobalProjectReadAccess(CurrentUserContextViewModel currentUser)
    {
        var authz = currentUser.Authorization ?? throw new InvalidOperationException(
            "AuthorizationSnapshot must be populated for this request.");

        if (authz.IsSuperAdmin)
        {
            return true;
        }

        return authz.GlobalPermissions.Any(PermissionKeys.GrantsProjectRead);
    }

    private static HashSet<int> BuildAccessibleProjectIds(CurrentUserContextViewModel currentUser)
    {
        var authz = currentUser.Authorization ?? throw new InvalidOperationException(
            "AuthorizationSnapshot must be populated for this request.");

        var fromSnapshot = authz.PerProjectPermissions
            .Where(kvp => kvp.Value.Any(PermissionKeys.GrantsProjectRead))
            .Select(kvp => kvp.Key);

        return currentUser.VisibleProjectIds.Concat(fromSnapshot).ToHashSet();
    }

    private sealed record FocusRecordDetailRow(
        int RecordId,
        int ProjectId,
        string ProjectCode,
        string ProjectName,
        string RecordNumber,
        string Title,
        string? Goal,
        int OwnerId,
        string Owner,
        string Subsystem,
        string State,
        DateTime CreatedAt,
        DateTime? Deadline);

    private sealed record RelevantRecordRow(
        int RecordId,
        int ProjectId,
        int OwnerId,
        int SubsystemId);

    private sealed record AuditRow(
        long Id,
        int? ActorOsobaId,
        string EntityType,
        string EntityId,
        string Action,
        DateTime CreatedAt);

    private sealed record CommentAuditRow(
        int CommentId,
        int RecordId,
        int ProjectId,
        string ProjectCode,
        string ProjectName,
        string RecordNumber,
        string RecordTitle,
        string? CommentText);

    private sealed record RecordAuditRow(
        int RecordId,
        int ProjectId,
        string ProjectCode,
        string ProjectName,
        string RecordNumber,
        string RecordTitle,
        string CategoryCode,
        string CategoryName,
        string? Cil);

    private sealed record MeetingAuditRow(
        int MeetingId,
        int ProjectId,
        string ProjectCode,
        string ProjectName,
        int MeetingNumber,
        DateTime Date,
        TimeOnly StartTime);
}
