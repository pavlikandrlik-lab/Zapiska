using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Diagnostics;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class HomeController : BaseController
{
    public HomeController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Dashboard");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // Po pádu běží akce už na /Home/Error. Původní cestu i výjimku drží feature,
        // kterou naplnil UseExceptionHandler — bez ní by diagnostika hlásila /Home/Error.
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var traceId = HttpContext.TraceIdentifier;

        if (feature?.Error is null)
        {
            return View(new ErrorViewModel { RequestId = traceId });
        }

        return View(new ErrorViewModel
        {
            RequestId = traceId,
            ErrorCode = "UNHANDLED_EXCEPTION",
            Message = "Požadavek se nepodařilo zpracovat.",
            DiagnosticLog = DiagnosticLogBuilder.Build(new DiagnosticLogRequest(
                TimestampUtc: GetUtcNow(),
                ErrorCode: "UNHANDLED_EXCEPTION",
                TraceId: traceId,
                RequestLine: $"{HttpContext.Request.Method} {feature.Path}",
                Message: "Neošetřená výjimka při zpracování požadavku.",
                Exception: feature.Error)),
        });
    }
}
