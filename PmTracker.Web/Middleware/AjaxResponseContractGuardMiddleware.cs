using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Middleware;

public sealed class AjaxResponseContractGuardMiddleware(RequestDelegate next, ILogger<AjaxResponseContractGuardMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldGuard(context.Request))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await next(context);

            var rawBody = await ReadBodyAsync(responseBuffer, context.RequestAborted);
            var contentType = ResolveContentType(context.Response);

            if (IsValidJsonContractBody(rawBody))
            {
                if (string.IsNullOrWhiteSpace(contentType))
                {
                    context.Response.ContentType = "application/json; charset=utf-8";
                }

                await CopyBufferAsync(responseBuffer, originalBody, context.RequestAborted);
                return;
            }

            var errorCode = string.IsNullOrWhiteSpace(rawBody)
                ? AjaxErrorCodes.EmptyAjaxResponse
                : AjaxErrorCodes.NonJsonResponse;
            var message = string.IsNullOrWhiteSpace(rawBody)
                ? "Server vrátil prázdnou odpověď pro AJAX požadavek."
                : "Server vrátil neočekávanou odpověď, která není validní JSON.";
            var traceId = ResolveTraceId(context);
            var diagnosticLog = BuildContractViolationDiagnosticLog(context, errorCode, traceId, rawBody, contentType, message);

            logger.LogError(
                "Ajax response contract violation. ErrorCode={ErrorCode} TraceId={TraceId} Status={StatusCode} ContentType={ContentType} DiagnosticLog={DiagnosticLog}",
                errorCode,
                traceId,
                context.Response.StatusCode,
                contentType,
                diagnosticLog);

            await WriteJsonErrorAsync(
                context,
                originalBody,
                message,
                errorCode,
                traceId,
                diagnosticLog,
                NormalizeErrorStatusCode(context.Response.StatusCode));
        }
        catch (Exception ex)
        {
            var traceId = ResolveTraceId(context);
            var diagnosticLog = BuildExceptionDiagnosticLog(context, traceId, ex);

            logger.LogError(
                ex,
                "Unhandled AJAX exception. ErrorCode={ErrorCode} TraceId={TraceId} DiagnosticLog={DiagnosticLog}",
                AjaxErrorCodes.UnexpectedServerError,
                traceId,
                diagnosticLog);

            await WriteJsonErrorAsync(
                context,
                originalBody,
                "Operaci se nepodařilo dokončit.",
                AjaxErrorCodes.UnexpectedServerError,
                traceId,
                diagnosticLog,
                StatusCodes.Status500InternalServerError);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool ShouldGuard(HttpRequest request)
    {
        var requestedWith = request.Headers["X-Requested-With"].ToString();
        if (!string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HttpMethods.IsPost(request.Method))
        {
            return true;
        }

        if (HttpMethods.IsGet(request.Method))
        {
            return request.Path.Value?.EndsWith("/App/KeepAlive", StringComparison.OrdinalIgnoreCase) == true;
        }

        return false;
    }

    private static async Task<string> ReadBodyAsync(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        stream.Position = 0;
        return text;
    }

    private static bool IsValidJsonContractBody(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(rawBody);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string ResolveContentType(HttpResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.ContentType))
        {
            return response.ContentType;
        }

        return response.Headers.TryGetValue("Content-Type", out var headerValues)
            ? headerValues.ToString()
            : string.Empty;
    }

    private static async Task CopyBufferAsync(Stream source, Stream target, CancellationToken cancellationToken)
    {
        source.Position = 0;
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task WriteJsonErrorAsync(
        HttpContext context,
        Stream originalBody,
        string message,
        string errorCode,
        string traceId,
        string diagnosticLog,
        int statusCode)
    {
        context.Response.Clear();
        context.Response.Body = originalBody;
        context.Response.StatusCode = statusCode;
        context.Response.Headers["X-Trace-Id"] = traceId;

        var payload = new ModalSubmitResultViewModel
        {
            Ok = false,
            Message = message,
            ErrorCode = errorCode,
            TraceId = traceId,
            DiagnosticLog = diagnosticLog,
            FieldErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        };

        await context.Response.WriteAsJsonAsync(payload, cancellationToken: context.RequestAborted);
    }

    private static int NormalizeErrorStatusCode(int originalStatusCode)
    {
        if (originalStatusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices)
        {
            return StatusCodes.Status500InternalServerError;
        }

        if (originalStatusCode <= 0)
        {
            return StatusCodes.Status500InternalServerError;
        }

        return originalStatusCode;
    }

    private static string ResolveTraceId(HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }

    private static string BuildContractViolationDiagnosticLog(
        HttpContext context,
        string errorCode,
        string traceId,
        string rawBody,
        string contentType,
        string message)
    {
        var builder = new StringBuilder(2048);
        builder.Append("TimestampUtc: ").AppendLine(DateTime.UtcNow.ToString("O"));
        builder.Append("ErrorCode: ").AppendLine(errorCode);
        builder.Append("TraceId: ").AppendLine(traceId);
        builder.AppendLine("ServerSource: AjaxResponseContractGuardMiddleware");
        builder.Append("Request: ")
            .Append(context.Request.Method)
            .Append(' ')
            .Append(context.Request.Path)
            .Append(context.Request.QueryString)
            .AppendLine();
        builder.Append("Endpoint: ").AppendLine(context.GetEndpoint()?.DisplayName ?? "-");
        builder.Append("Status: ")
            .Append(context.Response.StatusCode)
            .Append(' ')
            .AppendLine(ReasonPhrases.GetReasonPhrase(context.Response.StatusCode));
        builder.Append("ContentType: ").AppendLine(string.IsNullOrWhiteSpace(contentType) ? "-" : contentType);
        builder.Append("Message: ").AppendLine(message);
        AppendHeadersSection(builder, "ResponseHeaders", context.Response.Headers);
        AppendHeadersSection(builder, "RequestHeaders", context.Request.Headers);
        AppendFormValuesSection(builder, context.Request);
        builder.AppendLine("Body:");
        builder.AppendLine(string.IsNullOrWhiteSpace(rawBody) ? "<empty>" : rawBody);
        return builder.ToString().TrimEnd();
    }

    private static string BuildExceptionDiagnosticLog(HttpContext context, string traceId, Exception exception)
    {
        var builder = new StringBuilder(2048);
        builder.Append("TimestampUtc: ").AppendLine(DateTime.UtcNow.ToString("O"));
        builder.Append("ErrorCode: ").AppendLine(AjaxErrorCodes.UnexpectedServerError);
        builder.Append("TraceId: ").AppendLine(traceId);
        builder.AppendLine("ServerSource: AjaxResponseContractGuardMiddleware");
        builder.Append("Request: ")
            .Append(context.Request.Method)
            .Append(' ')
            .Append(context.Request.Path)
            .Append(context.Request.QueryString)
            .AppendLine();
        builder.Append("Endpoint: ").AppendLine(context.GetEndpoint()?.DisplayName ?? "-");
        builder.AppendLine("Details:");
        builder.AppendLine("Unhandled exception was caught by AJAX response contract guard middleware.");
        AppendHeadersSection(builder, "RequestHeaders", context.Request.Headers);
        AppendFormValuesSection(builder, context.Request);
        builder.AppendLine("Exception:");
        builder.AppendLine(exception.ToString());
        return builder.ToString().TrimEnd();
    }

    private static void AppendHeadersSection(StringBuilder builder, string title, IEnumerable<KeyValuePair<string, StringValues>> headers)
    {
        builder.Append(title).AppendLine(":");
        var hasAny = false;
        foreach (var header in headers.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            hasAny = true;
            builder.Append("  ")
                .Append(header.Key)
                .Append(": ")
                .AppendLine(header.Value.ToString());
        }

        if (!hasAny)
        {
            builder.AppendLine("  <none>");
        }
    }

    private static void AppendFormValuesSection(StringBuilder builder, HttpRequest request)
    {
        builder.AppendLine("RequestFormData:");

        if (!request.HasFormContentType)
        {
            builder.AppendLine("  <not-form-content-type>");
            return;
        }

        try
        {
            var form = request.Form;
            if (form.Count == 0)
            {
                builder.AppendLine("  <empty>");
                return;
            }

            foreach (var key in form.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append("  ")
                    .Append(key)
                    .Append("=")
                    .AppendLine(string.Join(" | ", form[key].ToArray()));
            }
        }
        catch (Exception ex)
        {
            builder.Append("  <form-read-error> ").AppendLine(ex.Message);
        }
    }
}
