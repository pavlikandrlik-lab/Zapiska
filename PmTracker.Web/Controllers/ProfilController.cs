using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Profile;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ProfilController : BaseController
{
    private readonly IProfileService _profileService;

    public ProfilController(
        IUserContextResolver userContextResolver,
        IProfileService profileService)
        : base(userContextResolver)
    {
        _profileService = profileService;
    }

    public IActionResult Index(int? projektId)
    {
        var model = _profileService.BuildProfilPage(CurrentUserContext, projektId);
        return View(model);
    }
}
