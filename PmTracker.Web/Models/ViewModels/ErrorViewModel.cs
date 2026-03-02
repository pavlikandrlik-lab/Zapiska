namespace PmTracker.Web.Models.ViewModels;

public sealed class ErrorViewModel
{
    public string? RequestId { get; init; }

    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
}
