using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;
using System.Diagnostics;
using System.Text;

namespace PmTracker.Web.Controllers;

public abstract class BaseController : Controller
{
    protected const string InvalidFormFallbackMessage = "Formulář obsahuje neplatné hodnoty.";

    private readonly IUserContextResolver _userContextResolver;

    protected CurrentUserContextViewModel CurrentUserContext { get; private set; } = null!;

    protected BaseController(IUserContextResolver userContextResolver)
    {
        _userContextResolver = userContextResolver;
    }

    protected DateTime GetLocalNow()
    {
        var timeProvider = HttpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        return timeProvider.GetLocalNow().LocalDateTime;
    }

    protected bool IsAjaxRequest()
    {
        var header = HttpContext.Request.Headers["X-Requested-With"].ToString();
        return string.Equals(header, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    protected bool WantsHtmlResponse()
    {
        if (IsAjaxRequest())
        {
            return false;
        }

        var accepts = HttpContext.Request.GetTypedHeaders().Accept;
        if (accepts is null || accepts.Count == 0)
        {
            return true;
        }

        return accepts.Any(item =>
        {
            var mediaType = item.MediaType.Value;
            return string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mediaType, "*/*", StringComparison.OrdinalIgnoreCase);
        });
    }

    private IActionResult AccessDeniedPageResult(int statusCode, string? message)
    {
        Response.StatusCode = statusCode;
        ViewData["Title"] = statusCode == StatusCodes.Status401Unauthorized
            ? "Ověření uživatele selhalo"
            : "Nemáte přístup do aplikace";
        ViewData["AccessDeniedStatusCode"] = statusCode;
        ViewData["AccessDeniedMessage"] = string.IsNullOrWhiteSpace(message)
            ? "Přístup k aplikaci nebylo možné ověřit."
            : message;
        return View("~/Views/Shared/AccessDenied.cshtml");
    }

    protected Dictionary<string, string[]> BuildModelStateFieldErrors()
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ModelState)
        {
            if (entry.Value is null || entry.Value.ValidationState == ModelValidationState.Valid)
            {
                continue;
            }

            var key = NormalizeModelStateKey(entry.Key);
            var messages = entry.Value.Errors
                .Select(error =>
                    !string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.ErrorMessage.Trim()
                        : error.Exception?.Message?.Trim())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Cast<string>()
                .ToList();

            if (messages.Count == 0)
            {
                var attemptedValue = entry.Value.AttemptedValue;
                if (string.IsNullOrWhiteSpace(attemptedValue) && entry.Value.RawValue is string[] rawArray)
                {
                    attemptedValue = string.Join(", ", rawArray.Where(value => !string.IsNullOrWhiteSpace(value)));
                }
                else if (string.IsNullOrWhiteSpace(attemptedValue) && entry.Value.RawValue is not null)
                {
                    attemptedValue = entry.Value.RawValue.ToString();
                }

                messages.Add(!string.IsNullOrWhiteSpace(attemptedValue)
                    ? $"Neplatná hodnota ({key}): \"{attemptedValue}\"."
                    : $"Neplatná hodnota ({key}).");
            }

            if (!result.TryGetValue(key, out var existing))
            {
                result[key] = messages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                continue;
            }

            existing.AddRange(messages);
            result[key] = existing.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        return result.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

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

    protected IActionResult ExecuteValidatedCommand(
        Func<bool> hasPermission,
        string invalidAjaxMessage,
        string invalidFallbackMessage,
        Func<IActionResult> onInvalidRedirect,
        Func<IActionResult> onSuccessRedirect,
        Func<IActionResult>? onAjaxSuccess,
        Action operation,
        Func<Exception, IActionResult>? onExceptionRedirect = null)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                return AjaxInvalidModelResult(invalidAjaxMessage);
            }

            var fieldErrors = BuildModelStateFieldErrors();
            TempData["ErrorMessage"] = $"{invalidAjaxMessage}: {JoinFieldErrors(fieldErrors, invalidFallbackMessage)}";
            return onInvalidRedirect();
        }

        return ExecuteCommand(
            hasPermission,
            onSuccessRedirect,
            onAjaxSuccess,
            operation,
            onExceptionRedirect);
    }

    protected IActionResult ExecuteCommand(
        Func<bool> hasPermission,
        Func<IActionResult> onSuccessRedirect,
        Func<IActionResult>? onAjaxSuccess,
        Action operation,
        Func<Exception, IActionResult>? onExceptionRedirect = null)
    {
        if (!hasPermission())
        {
            if (IsAjaxRequest())
            {
                return AjaxForbiddenResult();
            }

            return Forbid();
        }

        try
        {
            operation();
            if (IsAjaxRequest() && onAjaxSuccess is not null)
            {
                return onAjaxSuccess();
            }

            return onSuccessRedirect();
        }
        catch (Exception ex)
        {
            if (IsAjaxRequest())
            {
                if (ex is RecordValidationException validationException)
                {
                    return AjaxErrorResult(
                        validationException.Message,
                        validationException.ErrorCode,
                        validationException,
                        validationException.FieldErrors,
                        validationException.DiagnosticLog);
                }

                if (ex is InvalidOperationException invalidOperationException)
                {
                    return AjaxErrorResult(
                        invalidOperationException.Message,
                        AjaxErrorCodes.OperationFailed,
                        invalidOperationException);
                }

                return AjaxErrorResult(
                    "Operaci se nepodařilo dokončit.",
                    AjaxErrorCodes.UnexpectedServerError,
                    ex);
            }

            TempData["ErrorMessage"] = ex.Message;
            if (onExceptionRedirect is not null)
            {
                return onExceptionRedirect(ex);
            }

            return onSuccessRedirect();
        }
    }

    protected static string JoinFieldErrors(Dictionary<string, string[]> fieldErrors, string fallback)
    {
        var messages = fieldErrors.Values
            .SelectMany(values => values)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return messages.Count == 0 ? fallback : string.Join(" | ", messages);
    }

    private static string NormalizeModelStateKey(string rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return string.Empty;
        }

        var key = rawKey.Trim();
        var dotIndex = key.IndexOf('.');
        if (dotIndex <= 0)
        {
            return key;
        }

        var prefix = key[..dotIndex];
        if (prefix.Equals("command", StringComparison.OrdinalIgnoreCase))
        {
            return key[(dotIndex + 1)..];
        }

        if (prefix.EndsWith("command", StringComparison.OrdinalIgnoreCase))
        {
            return key[(dotIndex + 1)..];
        }

        return key;
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
                var joined = string.Join(" | ", form[key].ToArray());
                values[key] = joined;
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

        var formValues = ReadFormValuesForDiagnostics();
        AppendDictionarySection(builder, "FormValues:", formValues);

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
        var loggerFactory = HttpContext.RequestServices.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger(GetType().FullName ?? nameof(BaseController));
        if (logger is null)
        {
            return;
        }

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

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resolved = await _userContextResolver.ResolveAsync(context.HttpContext, context.HttpContext.RequestAborted);

        if (!resolved.IsSuccess || resolved.UserContext is null)
        {
            context.Result = WantsHtmlResponse()
                ? AccessDeniedPageResult(resolved.StatusCode, resolved.ErrorMessage)
                : resolved.StatusCode switch
                {
                    StatusCodes.Status401Unauthorized => new UnauthorizedObjectResult(new { error = resolved.ErrorMessage }),
                    _ => new ObjectResult(new { error = resolved.ErrorMessage }) { StatusCode = resolved.StatusCode }
                };
            return;
        }

        CurrentUserContext = resolved.UserContext;

        ViewBag.CurrentUserContext = CurrentUserContext;
        ViewBag.CurrentUserName = CurrentUserContext.DisplayName;
        ViewBag.CurrentUserEmail = CurrentUserContext.Email;
        ViewBag.CurrentUserOrg = CurrentUserContext.OrganizacniCelek;
        ViewBag.CurrentUserOrgCode = string.IsNullOrWhiteSpace(CurrentUserContext.OrganizacniCelekKod)
            ? CurrentUserContext.OrganizacniCelek
            : CurrentUserContext.OrganizacniCelekKod;
        ViewBag.CurrentUserRoles = CurrentUserContext.RoleKody;
        ViewBag.IsGlobalAdmin = CurrentUserContext.IsSuperAdmin;

        await next();
    }
}
