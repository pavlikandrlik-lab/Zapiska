namespace PmTracker.Web.Services.Data;

public static class AjaxErrorCodes
{
    public const string RequestValidationFailed = "REQUEST_VALIDATION_FAILED";
    public const string RecordValidationFailed = "RECORD_VALIDATION_FAILED";
    public const string OperationFailed = "OPERATION_FAILED";
    public const string UnexpectedServerError = "UNEXPECTED_SERVER_ERROR";
    public const string SessionExpired = "SESSION_EXPIRED";

    /// <summary>Spec 2026-09-17 §5 — záznam mezitím uložil jiný člověk.</summary>
    public const string RecordStale = "RECORD_STALE";
    public const string NonJsonResponse = "NON_JSON_RESPONSE";
    public const string EmptyAjaxResponse = "EMPTY_AJAX_RESPONSE";
}

/// <summary>
/// Spec 2026-09-17 §5 — záznam mezitím uložil jiný ČLOVĚK (ne automat).
/// Verzí je id posledního auditního zápisu; automat neaudituje, takže sem jeho zápisy nevedou.
/// </summary>
public sealed class RecordStaleException(string message) : Exception(message)
{
    public string ErrorCode => AjaxErrorCodes.RecordStale;
}

public sealed record RecordValidationIssue(
    string FieldKey,
    string Message,
    string Tab,
    string? Rule,
    string? Value);

public sealed class RecordValidationException : Exception
{
    public RecordValidationException(
        string message,
        IReadOnlyList<RecordValidationIssue> issues,
        string diagnosticLog)
        : base(message)
    {
        Issues = issues;
        DiagnosticLog = diagnosticLog;
        FieldErrors = BuildFieldErrors(issues);
    }

    public string ErrorCode => AjaxErrorCodes.RecordValidationFailed;
    public IReadOnlyList<RecordValidationIssue> Issues { get; }
    public string DiagnosticLog { get; }
    public Dictionary<string, string[]> FieldErrors { get; }

    private static Dictionary<string, string[]> BuildFieldErrors(IReadOnlyList<RecordValidationIssue> issues)
    {
        var grouped = issues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.FieldKey) && !string.IsNullOrWhiteSpace(issue.Message))
            .GroupBy(issue => issue.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(issue => issue.Message.Trim())
                    .Where(message => message.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return grouped;
    }
}
