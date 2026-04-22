using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Profile;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class ProfilController : BaseController
{
    private readonly IProfileService _profileService;

    public ProfilController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IProfileService profileService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _profileService = profileService;
    }

    public async Task<IActionResult> Index(int? projektId, CancellationToken ct)
    {
        var model = await _profileService.BuildProfilPageAsync(CurrentUserContext, projektId, ct);
        model.PageTitle = "Můj profil";
        return View(model);
    }
}
