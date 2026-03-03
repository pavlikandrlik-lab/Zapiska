using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Documentation;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("Dokumentace")]
public sealed class DokumentaceController : BaseController
{
    private readonly IDocumentationService _documentationService;

    public DokumentaceController(
        IPmTrackerDataStore dataStore,
        IUserContextResolver userContextResolver,
        IDocumentationService documentationService)
        : base(dataStore, userContextResolver)
    {
        _documentationService = documentationService;
    }

    [HttpGet("Technicka-dokumentace")]
    public IActionResult TechnickaDokumentace()
    {
        return RenderPage("technicka");
    }

    [HttpGet("Instalacni-prirucka")]
    public IActionResult InstalacniPrirucka()
    {
        // Backward-compatible route: installation docs are kept as markdown-only,
        // so in-app navigation points users to technical docs instead.
        return RedirectToAction(nameof(TechnickaDokumentace));
    }

    [HttpGet("Administracni-prirucka")]
    public IActionResult AdministracniPrirucka()
    {
        return RenderPage("admin");
    }

    [HttpGet("Uzivatelska-prirucka")]
    public IActionResult UzivatelskaPrirucka()
    {
        return RenderPage("uzivatelska");
    }

    [HttpGet("qa")]
    public IActionResult Qa()
    {
        return RenderPage("qa");
    }

    [HttpGet("Changelog")]
    public IActionResult Changelog()
    {
        return RenderPage("changelog");
    }

    private IActionResult RenderPage(string key)
    {
        var model = _documentationService.BuildPage(key);
        ViewData["Title"] = model.Title;
        return View("Index", model);
    }
}
