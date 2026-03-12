namespace PmTracker.Web.Models.ViewModels;

public sealed record class KeepAliveResultViewModel
{
    public required bool Ok { get; init; }
    public required string TraceId { get; init; }
    public string? ServerUtc { get; init; }
    public string? RequestVerificationToken { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
}
