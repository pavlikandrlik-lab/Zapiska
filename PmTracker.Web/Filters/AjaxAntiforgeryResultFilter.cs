using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Filters;

public sealed class AjaxAntiforgeryResultFilter(ILogger<AjaxAntiforgeryResultFilter> logger, TimeProvider timeProvider) : IAsyncAlwaysRunResultFilter
{
    private const string AntiForgeryFieldKey = "__RequestVerificationToken";
    private const string AntiForgeryMessage = "Bezpečnostní token formuláře vypršel nebo je neplatný. Obnovte stránku a akci opakujte.";

    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!IsAjaxRequest(context.HttpContext.Request) || !IsAntiforgeryValidationResult(context.Result))
        {
            return next();
        }

        var traceId = ResolveTraceId(context.HttpContext);
        var diagnosticLog = BuildDiagnosticLog(context, traceId);
        logger.LogWarning(
            "Ajax antiforgery validation failed. ErrorCode={ErrorCode} TraceId={TraceId} DiagnosticLog={DiagnosticLog}",
            AjaxErrorCodes.RequestValidationFailed,
            traceId,
            diagnosticLog);

        context.Result = new BadRequestObjectResult(new ModalSubmitResultViewModel
        {
            Ok = false,
            Message = AntiForgeryMessage,
            ErrorCode = AjaxErrorCodes.RequestValidationFailed,
            TraceId = traceId,
            FieldErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [AntiForgeryFieldKey] = new[]
                {
                    "Token formuláře není platný nebo vypršel."
                }
            }
        });

        return Task.CompletedTask;
    }

    private static bool IsAjaxRequest(HttpRequest request)
    {
        var header = request.Headers["X-Requested-With"].ToString();
        return string.Equals(header, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAntiforgeryValidationResult(IActionResult result)
    {
        return result is IAntiforgeryValidationFailedResult;
    }

    private static string ResolveTraceId(HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }

    private string BuildDiagnosticLog(ResultExecutingContext context, string traceId)
    {
        var request = context.HttpContext.Request;
        var builder = new StringBuilder(1024);
        builder.Append("TimestampUtc: ")
            .AppendLine(timeProvider.GetUtcNow().UtcDateTime.ToString("O"));
        builder.Append("ErrorCode: ")
            .AppendLine(AjaxErrorCodes.RequestValidationFailed);
        builder.Append("TraceId: ")
            .AppendLine(traceId);
        builder.Append("Request: ")
            .Append(request.Method)
            .Append(' ')
            .Append(request.Path)
            .Append(request.QueryString)
            .AppendLine();
        builder.Append("Accept: ")
            .AppendLine(request.Headers[HeaderNames.Accept].ToString());
        builder.Append("Message: ")
            .AppendLine(AntiForgeryMessage);
        builder.AppendLine("Details:");
        builder.AppendLine("ASP.NET Core antiforgery validation failed before action execution.");

        return builder.ToString().TrimEnd();
    }
}
