namespace PmTracker.Web.Services.Data;

public static class AjaxErrorCodes
{
    public const string RequestValidationFailed = "REQUEST_VALIDATION_FAILED";
    public const string RecordValidationFailed = "RECORD_VALIDATION_FAILED";
    public const string OperationFailed = "OPERATION_FAILED";
    public const string UnexpectedServerError = "UNEXPECTED_SERVER_ERROR";
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
