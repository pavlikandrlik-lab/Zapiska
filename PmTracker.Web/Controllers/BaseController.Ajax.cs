using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController
{
    protected BadRequestObjectResult AjaxInvalidModelResult(string? message = null)
    {
        var errorCode = AjaxErrorCodes.RequestValidationFailed;
        var fieldErrors = BuildModelStateFieldErrors();
        var responseMessage = string.IsNullOrWhiteSpace(message) ? InvalidFormFallbackMessage : message;
        return BadRequest(BuildAjaxFailurePayload(
            responseMessage,
            errorCode,
            fieldErrors,
            details: "ModelState validation failed.",
            exception: null,
            logLevel: LogLevel.Warning));
    }

    protected BadRequestObjectResult AjaxErrorResult(
        string message,
        string errorCode = AjaxErrorCodes.OperationFailed,
        Exception? exception = null,
        Dictionary<string, string[]>? fieldErrors = null,
        string? details = null)
    {
        var resolvedFieldErrors = fieldErrors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        return BadRequest(BuildAjaxFailurePayload(
            message,
            errorCode,
            resolvedFieldErrors,
            details,
            exception,
            exception is null ? LogLevel.Warning : LogLevel.Error));
    }

    protected ObjectResult AjaxForbiddenResult(string message = "Nemáte oprávnění k provedení této operace.")
    {
        var errorCode = AjaxErrorCodes.OperationFailed;
        var fieldErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        return StatusCode(StatusCodes.Status403Forbidden, BuildAjaxFailurePayload(
            message,
            errorCode,
            fieldErrors,
            details: "Permission check failed for AJAX request.",
            exception: null,
            logLevel: LogLevel.Warning));
    }

    protected JsonResult AjaxSuccessResult(
        string refreshScope,
        string? refreshUrl = null,
        int? projectId = null,
        int? recordId = null,
        int? meetingId = null,
        string? uiContext = null,
        string? tab = null,
        string? ciselnikKey = null,
        string? message = null)
    {
        return Json(new ModalSubmitResultViewModel
        {
            Ok = true,
            Message = message,
            RefreshScope = refreshScope,
            RefreshUrl = refreshUrl,
            ProjectId = projectId,
            RecordId = recordId,
            MeetingId = meetingId,
            UiContext = uiContext,
            Tab = tab,
            CiselnikKey = ciselnikKey
        });
    }

    private string ResolveTraceId()
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier))
        {
            return HttpContext.TraceIdentifier;
        }

        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }

    private Dictionary<string, string> ReadFormValuesForDiagnostics()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!HttpContext.Request.HasFormContentType)
        {
            return values;
        }

        try
        {
            var form = HttpContext.Request.Form;
            foreach (var key in form.Keys)
            {
                values[key] = string.Join(" | ", form[key].ToArray());
            }
        }
        catch (Exception ex)
        {
            values["<form-read-error>"] = ex.Message;
        }

        return values;
    }

    private static void AppendDictionarySection(StringBuilder builder, string title, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        builder.AppendLine(title);
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("  ")
                .Append(pair.Key)
                .Append(": ")
                .AppendLine(pair.Value);
        }
    }

    private static void AppendFieldErrorSection(StringBuilder builder, IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        if (fieldErrors.Count == 0)
        {
            return;
        }

        builder.AppendLine("FieldErrors:");
        foreach (var pair in fieldErrors.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var messages = pair.Value
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (messages.Length == 0)
            {
                continue;
            }

            builder.Append("  ")
                .Append(pair.Key)
                .Append(": ")
                .AppendLine(string.Join(" | ", messages));
        }
    }

    private string BuildDiagnosticLog(
        string errorCode,
        string traceId,
        string message,
        IReadOnlyDictionary<string, string[]> fieldErrors,
        string? details,
        Exception? exception)
    {
        var builder = new StringBuilder(2048);
        builder.Append("TimestampUtc: ")
            .AppendLine(DateTime.UtcNow.ToString("O"));
        builder.Append("ErrorCode: ")
            .AppendLine(errorCode);
        builder.Append("TraceId: ")
            .AppendLine(traceId);
        builder.Append("Request: ")
            .Append(HttpContext.Request.Method)
            .Append(' ')
            .Append(HttpContext.Request.Path)
            .Append(HttpContext.Request.QueryString)
            .AppendLine();
        builder.Append("Message: ")
            .AppendLine(message);

        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.AppendLine("Details:");
            builder.AppendLine(details.Trim());
        }

        AppendFieldErrorSection(builder, fieldErrors);
        AppendDictionarySection(builder, "FormValues:", ReadFormValuesForDiagnostics());

        if (exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(exception.ToString());
        }

        return builder.ToString().TrimEnd();
    }

    private ModalSubmitResultViewModel BuildAjaxFailurePayload(
        string message,
        string errorCode,
        Dictionary<string, string[]> fieldErrors,
        string? details,
        Exception? exception,
        LogLevel logLevel)
    {
        var traceId = ResolveTraceId();
        var diagnosticLog = BuildDiagnosticLog(
            errorCode,
            traceId,
            message,
            fieldErrors,
            details,
            exception);
        LogAjaxFailure(
            logLevel,
            errorCode,
            traceId,
            message,
            fieldErrors,
            diagnosticLog,
            exception);

        return new ModalSubmitResultViewModel
        {
            Ok = false,
            Message = message,
            ErrorCode = errorCode,
            TraceId = traceId,
            DiagnosticLog = diagnosticLog,
            FieldErrors = fieldErrors
        };
    }

    private void LogAjaxFailure(
        LogLevel level,
        string errorCode,
        string traceId,
        string message,
        IReadOnlyDictionary<string, string[]> fieldErrors,
        string diagnosticLog,
        Exception? exception)
    {
        var logger = _loggerFactory.CreateLogger(GetType().FullName ?? nameof(BaseController));
        var fieldErrorCount = fieldErrors.Values.Sum(values => values.Length);
        if (level == LogLevel.Error)
        {
            logger.LogError(
                exception,
                "Ajax failure. ErrorCode={ErrorCode} TraceId={TraceId} Message={Message} FieldErrorCount={FieldErrorCount} DiagnosticLog={DiagnosticLog}",
                errorCode,
                traceId,
                message,
                fieldErrorCount,
                diagnosticLog);
            return;
        }

        logger.LogWarning(
            "Ajax failure. ErrorCode={ErrorCode} TraceId={TraceId} Message={Message} FieldErrorCount={FieldErrorCount} DiagnosticLog={DiagnosticLog}",
            errorCode,
            traceId,
            message,
            fieldErrorCount,
            diagnosticLog);
    }
}
