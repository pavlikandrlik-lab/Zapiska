namespace PmTracker.Web.Models.ViewModels;

public sealed class PageHeaderViewModel
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string? BackUrl { get; init; }
    public string? BackLabel { get; init; }
    public string? Badge { get; init; }
    public bool UseRecordEditorBackNavigation { get; init; }
}
