using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ObsazeniController : BaseController
{
    public ObsazeniController(IPmTrackerDataStore dataStore, IUserContextResolver userContextResolver)
        : base(dataStore, userContextResolver)
    {
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Projekty");
    }
}
