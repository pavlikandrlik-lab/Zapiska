using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Režim „Zobrazit jako“: superadmin prochází aplikaci s právy jiné osoby, jen pro čtení.
/// Superadmin není permission klíč, proto body-check (allowlist v AuthorizationPolicyEnforcementTests).
/// </summary>
[Route("PohledJako")]
public sealed class PohledJakoController : BaseController
{
    private readonly IViewAsService _viewAsService;
    private readonly IProjectService _projectService;

    public PohledJakoController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IViewAsService viewAsService,
        IProjectService projectService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _viewAsService = viewAsService;
        _projectService = projectService;
    }

    private bool CanStartViewAs => CurrentUserContext.IsSuperAdmin && CurrentUserContext.ViewAs is null;

    [HttpGet("Vyber")]
    public IActionResult Vyber()
    {
        if (!CanStartViewAs)
        {
            return Forbid();
        }

        return View("VyberModal", new ViewAsModalViewModel
        {
            SearchUrl = Url.Action(nameof(HledatOsoby), WithAsUserRoute()) ?? "/PohledJako/HledatOsoby"
        });
    }

    [HttpGet("HledatOsoby")]
    public async Task<IActionResult> HledatOsoby([FromQuery(Name = "q")] string? query, CancellationToken ct)
    {
        if (!CanStartViewAs)
        {
            return Forbid();
        }

        var results = await _projectService.SearchProjectMemberCandidatesAsync(query ?? string.Empty, ct);
        return Json(new PersonPickerSearchResponseViewModel
        {
            Results = results.Where(x => x.Id != CurrentUserContext.OsobaId).ToList()
        });
    }

    [HttpPost("Zahajit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [ViewAsControl]
    public async Task<IActionResult> Zahajit([FromForm] int osobaId, CancellationToken ct)
    {
        if (CurrentUserContext.ViewAs is not null)
        {
            return AjaxErrorResult(ViewAsMessages.AlreadyActive, AjaxErrorCodes.ViewAsInvalidTarget);
        }

        if (!CurrentUserContext.IsSuperAdmin)
        {
            return AjaxForbiddenResult(ViewAsMessages.OnlySuperAdmin);
        }

        var result = await _viewAsService.StartAsync(HttpContext, CurrentUserContext.OsobaId, osobaId, ct);
        return result switch
        {
            ViewAsStartResult.Started => AjaxSuccessResult(
                "page",
                Url.Action("Index", "Dashboard", WithAsUserRoute()) ?? "/"),
            ViewAsStartResult.SelfTarget => TargetError(ViewAsMessages.SelfTarget),
            ViewAsStartResult.TargetNotFound => TargetError(ViewAsMessages.TargetNotFound),
            _ => AjaxForbiddenResult(ViewAsMessages.OnlySuperAdmin)
        };
    }

    [HttpPost("Ukoncit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [ViewAsControl]
    public async Task<IActionResult> Ukoncit(CancellationToken ct)
    {
        var viewAs = CurrentUserContext.ViewAs;
        await _viewAsService.StopAsync(
            HttpContext,
            viewAs?.RealOsobaId ?? CurrentUserContext.OsobaId,
            viewAs is null ? null : CurrentUserContext.OsobaId,
            ct);

        var back = LocalReferer.Resolve(Request);
        return back is not null
            ? LocalRedirect(back)
            : RedirectToAction("Index", "Dashboard", WithAsUserRoute());
    }

    private BadRequestObjectResult TargetError(string message)
        => AjaxErrorResult(
            message,
            AjaxErrorCodes.ViewAsInvalidTarget,
            fieldErrors: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["OsobaId"] = [message]
            });
}
