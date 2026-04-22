using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using PmTracker.Web.Middleware;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public abstract partial class BaseController : Controller
{
    protected const string InvalidFormFallbackMessage = "Formulář obsahuje neplatné hodnoty.";

    private readonly IUserContextResolver _userContextResolver;
    private readonly TimeProvider _timeProvider;
    private readonly ILoggerFactory _loggerFactory;

    protected CurrentUserContextViewModel CurrentUserContext { get; private set; } = null!;

    protected BaseController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        _userContextResolver = userContextResolver;
        _timeProvider = timeProvider;
        _loggerFactory = loggerFactory;
    }

    protected DateTime GetLocalNow()
    {
        return _timeProvider.GetLocalNow().LocalDateTime;
    }

    protected T AttachCurrentUser<T>(T model)
        where T : BaseViewModel
    {
        model.CurrentUserContext = CurrentUserContext;
        return model;
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

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // HIGH-1 fix: UserContextMiddleware běží před UseAuthorization() a cachuje
        // výsledek resolveru do HttpContext.Items. Čteme cache, aby se resolver nevolal
        // podruhé (vyhne se duplicitní DB práci). Pokud middleware resolve přeskočil
        // (anonymous request), spustíme ho ručně zde — získáme rich error result pro
        // AccessDenied rendering / JSON ProblemDetails.
        var resolved = context.HttpContext.Items[UserContextMiddleware.UserContextCacheKey] as UserContextResolutionResult
            ?? await _userContextResolver.ResolveAsync(context.HttpContext, context.HttpContext.RequestAborted);

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
        ViewData["NavPermissions"] = new NavPermissionsViewModel
        {
            CanViewPeople = CurrentUserContext.HasPermissionPrefix(PermissionKeys.PeoplePrefix),
            CanViewCiselniky = CurrentUserContext.HasPermissionPrefix(PermissionKeys.CiselnikyPrefix),
            CanViewSettings = CurrentUserContext.HasPermissionPrefix(PermissionKeys.SettingsPrefix),
            CurrentUserDisplayName = CurrentUserContext.DisplayName,
            CurrentUserEmail = CurrentUserContext.Email,
            CurrentUserOrg = CurrentUserContext.OrganizacniCelek,
            CurrentUserOrgCode = string.IsNullOrWhiteSpace(CurrentUserContext.OrganizacniCelekKod)
                ? CurrentUserContext.OrganizacniCelek
                : CurrentUserContext.OrganizacniCelekKod,
            CurrentUserRoles = CurrentUserContext.RoleKody
        };

        await next();
    }
}
