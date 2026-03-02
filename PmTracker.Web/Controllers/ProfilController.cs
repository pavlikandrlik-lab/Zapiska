using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ProfilController : BaseController
{
    public ProfilController(IPmTrackerDataStore dataStore, IUserContextResolver userContextResolver)
        : base(dataStore, userContextResolver)
    {
    }

    public IActionResult Index(int? projektId)
    {
        var model = DataStore.BuildProfilPage(CurrentUserContext, projektId);
        return View(model);
    }
}
