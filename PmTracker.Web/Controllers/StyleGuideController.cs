using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Living style guide — přehled pm-* TagHelper komponent pro vývojáře.
/// Přístup: kdokoli přihlášený (nejde o citlivá data).
/// </summary>
public sealed class StyleGuideController : BaseController
{
    public StyleGuideController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
    }

    [HttpGet]
    public IActionResult Index() => View();
}
