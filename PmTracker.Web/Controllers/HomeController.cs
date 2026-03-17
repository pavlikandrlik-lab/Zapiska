using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class HomeController : BaseController
{
    public HomeController(IUserContextResolver userContextResolver)
        : base(userContextResolver)
    {
    }

    public IActionResult Index()
    {
        return RedirectToAction("Index", "Projekty");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
