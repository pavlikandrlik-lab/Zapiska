using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public abstract class BaseController : Controller
{
    private readonly IPmTrackerDataStore _dataStore;
    private readonly IUserContextResolver _userContextResolver;

    protected CurrentUserContextViewModel CurrentUserContext { get; private set; } = null!;

    protected BaseController(IPmTrackerDataStore dataStore, IUserContextResolver userContextResolver)
    {
        _dataStore = dataStore;
        _userContextResolver = userContextResolver;
    }

    protected IPmTrackerDataStore DataStore => _dataStore;

    protected bool IsAjaxRequest()
    {
        var header = HttpContext.Request.Headers["X-Requested-With"].ToString();
        return string.Equals(header, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
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
        return BadRequest(new ModalSubmitResultViewModel
        {
            Ok = false,
            Message = string.IsNullOrWhiteSpace(message) ? "Formulář obsahuje neplatné hodnoty." : message,
            FieldErrors = BuildModelStateFieldErrors()
        });
    }

    protected BadRequestObjectResult AjaxErrorResult(string message)
    {
        return BadRequest(new ModalSubmitResultViewModel
        {
            Ok = false,
            Message = message
        });
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
                return AjaxErrorResult(ex.Message);
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

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var resolved = _userContextResolver.ResolveAsync(context.HttpContext, context.HttpContext.RequestAborted)
            .GetAwaiter()
            .GetResult();

        if (!resolved.IsSuccess || resolved.UserContext is null)
        {
            context.Result = resolved.StatusCode switch
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

        base.OnActionExecuting(context);
    }
}
