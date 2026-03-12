using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("App")]
public sealed class AppController : Controller
{
    private readonly IAntiforgery _antiforgery;
    private readonly IUserContextResolver _userContextResolver;

    public AppController(
        IAntiforgery antiforgery,
        IUserContextResolver userContextResolver)
    {
        _antiforgery = antiforgery;
        _userContextResolver = userContextResolver;
    }

    [HttpGet("KeepAlive")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> KeepAlive(CancellationToken cancellationToken)
    {
        var traceId = ResolveTraceId(HttpContext);
        var resolution = await _userContextResolver.ResolveAsync(HttpContext, cancellationToken);
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

        var antiforgeryTokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Json(new KeepAliveResultViewModel
        {
            Ok = true,
            TraceId = traceId,
            ServerUtc = DateTime.UtcNow.ToString("O"),
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
