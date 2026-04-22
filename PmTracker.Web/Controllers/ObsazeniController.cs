using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class ObsazeniController : BaseController
{
    public ObsazeniController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Projekty");
    }
}
