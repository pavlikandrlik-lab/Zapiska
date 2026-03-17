using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ObsazeniController : BaseController
{
    public ObsazeniController(IUserContextResolver userContextResolver)
        : base(userContextResolver)
    {
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Projekty");
    }
}
