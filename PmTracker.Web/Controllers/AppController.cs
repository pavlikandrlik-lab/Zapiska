using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("App")]
public sealed class AppController : Controller
{
    private readonly IAntiforgery _antiforgery;
    private readonly IUserContextResolver _userContextResolver;
    private readonly TimeProvider _timeProvider;
    private readonly IRecordEditLockService _editLockService;
    private readonly ILogger<AppController> _logger;

    public AppController(
        IAntiforgery antiforgery,
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        IRecordEditLockService editLockService,
        ILogger<AppController> logger)
    {
        _antiforgery = antiforgery;
        _userContextResolver = userContextResolver;
        _timeProvider = timeProvider;
        _editLockService = editLockService;
        _logger = logger;
    }

    [HttpGet("KeepAlive")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> KeepAlive(int? zaznamId, CancellationToken ct)
    {
        var traceId = ResolveTraceId(HttpContext);
        var resolution = await _userContextResolver.ResolveAsync(HttpContext, ct);
        if (!resolution.IsSuccess || resolution.UserContext is null)
        {
            var message = string.IsNullOrWhiteSpace(resolution.ErrorMessage)
                ? "Relace vypršela nebo nebylo možné ověřit uživatele."
                : resolution.ErrorMessage;
            var statusCode = resolution.StatusCode == StatusCodes.Status401Unauthorized
                ? StatusCodes.Status401Unauthorized
                : StatusCodes.Status403Forbidden;

            return StatusCode(statusCode, new KeepAliveResultViewModel
            {
                Ok = false,
                TraceId = traceId,
                ErrorCode = AjaxErrorCodes.SessionExpired,
                Message = message
            });
        }

        // Spec 2026-09-17 §4.2 — heartbeat zámku karty. Obnoví jen VLASTNÍ zámek
        // (filtr na osoba_id je ve službě), takže podvržené id cizí zámek neprodlouží.
        //
        // Best-effort: keep-alive drží relaci a jeho selhání uživateli hlásí vypršenou relaci.
        // Zámek je proti tomu vedlejší — když se heartbeat nepovede, nejhorší následek je
        // vypršení zámku po TTL, a to nesmí shodit celý keep-alive.
        if (zaznamId is > 0)
        {
            try
            {
                await _editLockService.HeartbeatAsync(zaznamId.Value, resolution.UserContext.OsobaId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Heartbeat zámku záznamu {ZaznamId} selhal; zámek vyprší TTL.", zaznamId.Value);
            }
        }

        var antiforgeryTokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Json(new KeepAliveResultViewModel
        {
            Ok = true,
            TraceId = traceId,
            ServerUtc = _timeProvider.GetUtcNow().UtcDateTime.ToString("O"),
            RequestVerificationToken = antiforgeryTokens.RequestToken
        });
    }

    private static string ResolveTraceId(HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }
}
