using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
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
            .AppendLine(_timeProvider.GetUtcNow().UtcDateTime.ToString("O"));
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

        if (exception is not null)
        {
            builder.AppendLine("Exception:");
            builder.AppendLine(exception.ToString());
            AppendSqlAndEfDetails(builder, exception);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// FIX 2026-05-04: <see cref="Exception.ToString"/> sice projde InnerException řetězec, ale
    /// <see cref="SqlException"/> má bohatou diagnostiku (Number, State, Class, Server, Procedure,
    /// LineNumber + <see cref="SqlException.Errors"/> kolekci) která se v default ToString nevypisuje.
    /// EF Core <see cref="DbUpdateException"/> navíc drží <see cref="DbUpdateException.Entries"/>
    /// s entitami které selhaly při SaveChanges. Tato pomocná metoda projde celý řetězec a vypíše
    /// vše co user potřebuje pro debugging "UNEXPECTED_SERVER_ERROR" pádů (typicky FK violation,
    /// unique constraint, NOT NULL, deadlock, schema drift).
    /// </summary>
    private static void AppendSqlAndEfDetails(StringBuilder builder, Exception rootException)
    {
        var sectionHeaderEmitted = false;
        var current = rootException;
        var depth = 0;
        while (current is not null && depth < 10)
        {
            if (current is SqlException sqlEx)
            {
                if (!sectionHeaderEmitted)
                {
                    builder.AppendLine("SqlServer/EFCore details:");
                    sectionHeaderEmitted = true;
                }
                builder.Append("  [SqlException @ depth=")
                    .Append(depth)
                    .Append("] Number=")
                    .Append(sqlEx.Number)
                    .Append(" State=")
                    .Append(sqlEx.State)
                    .Append(" Class=")
                    .Append(sqlEx.Class)
                    .Append(" Server=")
                    .Append(sqlEx.Server ?? "(null)")
                    .Append(" Procedure=")
                    .Append(string.IsNullOrEmpty(sqlEx.Procedure) ? "(none)" : sqlEx.Procedure)
                    .Append(" LineNumber=")
                    .AppendLine(sqlEx.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
                builder.Append("    Message: ").AppendLine(sqlEx.Message);
                if (sqlEx.Errors is { Count: > 0 } errs)
                {
                    for (var i = 0; i < errs.Count; i++)
                    {
                        var err = errs[i];
                        builder.Append("    Errors[").Append(i).Append("]: Number=")
                            .Append(err.Number).Append(" State=").Append(err.State)
                            .Append(" Class=").Append(err.Class)
                            .Append(" Line=").Append(err.LineNumber)
                            .Append(" Procedure=")
                            .Append(string.IsNullOrEmpty(err.Procedure) ? "(none)" : err.Procedure)
                            .Append(" | ").AppendLine(err.Message);
                    }
                }
            }

            if (current is DbUpdateException efEx)
            {
                if (!sectionHeaderEmitted)
                {
                    builder.AppendLine("SqlServer/EFCore details:");
                    sectionHeaderEmitted = true;
                }
                builder.Append("  [DbUpdateException @ depth=")
                    .Append(depth)
                    .Append("] EntryCount=")
                    .AppendLine(efEx.Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var entryIndex = 0;
                foreach (var entry in efEx.Entries)
                {
                    if (entryIndex >= 5)
                    {
                        builder.AppendLine("    … (truncated, additional entries omitted)");
                        break;
                    }
                    string keyDescription;
                    try
                    {
                        var key = entry.Metadata.FindPrimaryKey();
                        keyDescription = key is null
                            ? "(no PK metadata)"
                            : string.Join(",", key.Properties.Select(p =>
                                $"{p.Name}={entry.Property(p.Name).CurrentValue ?? "(null)"}"));
                    }
                    catch (Exception readEx)
                    {
                        keyDescription = $"(key read failed: {readEx.GetType().Name})";
                    }
                    builder.Append("    Entry[").Append(entryIndex).Append("] ")
                        .Append(entry.Metadata.ClrType.Name)
                        .Append(" State=").Append(entry.State)
                        .Append(" PK=").AppendLine(keyDescription);
                    entryIndex++;
                }
            }

            current = current.InnerException;
            depth++;
        }
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
