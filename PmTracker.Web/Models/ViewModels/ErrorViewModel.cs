namespace PmTracker.Web.Models.ViewModels;

public sealed class ErrorViewModel
{
    public string? RequestId { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }

    /// <summary>Kopírovatelný výpis ve stejném formátu, jaký ukazují chyby v modalech.</summary>
    public string? DiagnosticLog { get; init; }

    public bool ShowRequestId => !string.IsNullOrWhiteSpace(RequestId);
    public bool ShowDiagnostics => !string.IsNullOrWhiteSpace(DiagnosticLog);
}
