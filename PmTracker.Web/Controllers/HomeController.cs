using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Home;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class HomeController : BaseController
{
    private readonly IHomeDashboardService _homeDashboardService;

    public HomeController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IHomeDashboardService homeDashboardService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _homeDashboardService = homeDashboardService;
    }

    public IActionResult Index()
    {
        var model = AttachCurrentUser(_homeDashboardService.BuildDashboard(CurrentUserContext));
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
