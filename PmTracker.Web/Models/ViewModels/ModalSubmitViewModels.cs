namespace PmTracker.Web.Models.ViewModels;

public sealed class ModalSubmitResultViewModel
{
    public bool Ok { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public string? TraceId { get; init; }
    public string? DiagnosticLog { get; init; }
    public Dictionary<string, string[]> FieldErrors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? RefreshScope { get; init; }
    public string? RefreshUrl { get; init; }
    public int? ProjectId { get; init; }
    public int? RecordId { get; init; }
    public int? MeetingId { get; init; }
    public string? UiContext { get; init; }
    public string? Tab { get; init; }
    public string? CiselnikKey { get; init; }
}
