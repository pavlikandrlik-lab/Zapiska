namespace PmTracker.Web.Models.ViewModels;

public sealed class DashboardPageViewModel : BaseViewModel
{
    public string PageTitle { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public required DashboardHeroViewModel Hero { get; init; }
    public required IReadOnlyList<DashboardSectionViewModel> Sections { get; init; }
}

public sealed class DashboardHeroViewModel
{
    public string Eyebrow { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public required IReadOnlyList<DashboardStatViewModel> Stats { get; init; }
}

public sealed class DashboardStatViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class DashboardSectionViewModel
{
    public string Key { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public required IReadOnlyList<DashboardCardViewModel> Cards { get; init; }
}

public sealed class DashboardCardViewModel
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Badge { get; init; }
    public string? Meta { get; init; }
    public string? Controller { get; init; }
    public string? Action { get; init; }
    public bool IsPlaceholder { get; init; }
    public bool IsPrimary { get; init; }
}
