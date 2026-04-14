namespace PmTracker.Web.Models.ViewModels;

public sealed class DashboardPageViewModel : BaseViewModel
{
    public string PageTitle { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string FocusPanelUrl { get; init; } = string.Empty;
    public string MeetingsPanelUrl { get; init; } = string.Empty;
    public string NewsPanelUrl { get; init; } = string.Empty;
}

public sealed class DashboardFocusPanelViewModel
{
    public int TotalCount { get; init; }
    public string ListUrl { get; init; } = string.Empty;
    public IReadOnlyList<DashboardFocusItemViewModel> Items { get; init; } = Array.Empty<DashboardFocusItemViewModel>();
}

public sealed class DashboardMeetingsPanelViewModel
{
    public int TotalCount { get; init; }
    public string ListUrl { get; init; } = string.Empty;
    public IReadOnlyList<DashboardMeetingItemViewModel> Items { get; init; } = Array.Empty<DashboardMeetingItemViewModel>();
}

public sealed class DashboardNewsPanelViewModel
{
    public int LoadedCount { get; init; }
    public int TotalCount { get; init; }
    public bool CanLoadMore { get; init; }
    public string? LoadMoreUrl { get; init; }
    public string ListUrl { get; init; } = string.Empty;
    public IReadOnlyList<DashboardNewsItemViewModel> Items { get; init; } = Array.Empty<DashboardNewsItemViewModel>();
}

public sealed class DashboardFocusListPageViewModel : BaseViewModel
{
    public string PageTitle { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string? BackUrl { get; init; }
    public string? BackLabel { get; init; }
    public IReadOnlyList<DashboardFocusItemViewModel> Items { get; init; } = Array.Empty<DashboardFocusItemViewModel>();
}

public sealed class DashboardMeetingsListPageViewModel : BaseViewModel
{
    public string PageTitle { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string? BackUrl { get; init; }
    public string? BackLabel { get; init; }
    public IReadOnlyList<DashboardMeetingItemViewModel> Items { get; init; } = Array.Empty<DashboardMeetingItemViewModel>();
}

public sealed class DashboardNewsListPageViewModel : BaseViewModel
{
    public string PageTitle { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string? BackUrl { get; init; }
    public string? BackLabel { get; init; }
    public int LoadedCount { get; init; }
    public int TotalCount { get; init; }
    public bool CanLoadMore { get; init; }
    public string? LoadMoreUrl { get; init; }
    public IReadOnlyList<DashboardNewsItemViewModel> Items { get; init; } = Array.Empty<DashboardNewsItemViewModel>();
}

public sealed class DashboardFocusItemViewModel
{
    public int RecordId { get; init; }
    public int ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string RecordNumber { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Goal { get; init; }
    public string Owner { get; init; } = string.Empty;
    public string Subsystem { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? Deadline { get; init; }
    public string DetailUrl { get; set; } = string.Empty;
}

public sealed class DashboardMeetingItemViewModel
{
    public int MeetingId { get; init; }
    public int ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public required JednaniListItemViewModel Meeting { get; init; }
    public string DetailUrl { get; set; } = string.Empty;
    public string PrintPdfUrl { get; set; } = string.Empty;
    public string PrintWordUrl { get; set; } = string.Empty;
}

public sealed class DashboardNewsItemViewModel
{
    public long AuditLogId { get; init; }
    public int? ProjectId { get; init; }
    public int? RecordId { get; init; }
    public int? MeetingId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string EventLabel { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ProjectLabel { get; init; } = string.Empty;
    public string? RecordNumber { get; init; }
    public DateTime CreatedAt { get; init; }
    public string DetailUrl { get; set; } = string.Empty;
    public bool OpensComments { get; init; }
}
