namespace PmTracker.Web.Models.ViewModels;

public sealed class DocumentationPageViewModel
{
    public required string Key { get; init; }
    public required string SectionLabel { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string CanonicalPath { get; init; }
    public required string HtmlContent { get; init; }
    public IReadOnlyList<DocumentationNavItemViewModel> Navigation { get; init; } = Array.Empty<DocumentationNavItemViewModel>();
    public IReadOnlyList<DocumentationTocItemViewModel> TocItems { get; init; } = Array.Empty<DocumentationTocItemViewModel>();
}

public sealed class DocumentationNavItemViewModel
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Path { get; init; }
    public bool IsActive { get; init; }
}

public sealed class DocumentationTocItemViewModel
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public int Level { get; init; }
}
