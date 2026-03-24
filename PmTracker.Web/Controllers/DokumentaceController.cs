using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Services.Documentation;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("Dokumentace")]
public sealed class DokumentaceController : BaseController
{
    private readonly IDocumentationService _documentationService;

    public DokumentaceController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IDocumentationService documentationService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _documentationService = documentationService;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(StromDokumentace));
    }

    [HttpGet("Technicka/Strom-dokumentace")]
    public IActionResult StromDokumentace()
    {
        return RenderPage("tech-documentation-tree");
    }

    [HttpGet("Technicka/Systemovy-kontext")]
    public IActionResult SystemovyKontext()
    {
        return RenderPage("tech-system-context");
    }

    [HttpGet("Technicka/Architektura")]
    public IActionResult Architektura()
    {
        return RenderPage("tech-architecture");
    }

    [HttpGet("Technicka/Runtime-konfigurace")]
    public IActionResult RuntimeKonfigurace()
    {
        return RenderPage("tech-runtime-configuration");
    }

    [HttpGet("Technicka/Instalace-a-deployment-iis")]
    public IActionResult InstalaceADeploymentIis()
    {
        return RenderPage("tech-installation-deployment-iis");
    }

    [HttpGet("Technicka/Web-server-iis")]
    public IActionResult WebServerIis()
    {
        return RenderPage("tech-web-server-iis-config");
    }

    [HttpGet("Technicka/Databaze-bootstrap-a-migrace")]
    public IActionResult DatabazeBootstrapAMigrace()
    {
        return RenderPage("tech-database-bootstrap-migrations");
    }

    [HttpGet("Technicka/Bezpecnost-a-opravneni")]
    public IActionResult BezpecnostAOpravneni()
    {
        return RenderPage("tech-security-authz");
    }

    [HttpGet("Technicka/Provozni-runbooky")]
    public IActionResult ProvozniRunbooky()
    {
        return RenderPage("tech-operations-runbooks");
    }

    [HttpGet("Technicka/Testovani-a-kvalita")]
    public IActionResult TestovaniAKvalita()
    {
        return RenderPage("tech-testing-quality");
    }

    [HttpGet("Technicka/Troubleshooting-a-recovery")]
    public IActionResult TroubleshootingARecovery()
    {
        return RenderPage("tech-troubleshooting-recovery");
    }

    [HttpGet("Uzivatelska-prirucka")]
    public IActionResult UzivatelskaPrirucka()
    {
        return RenderPage("user-guide");
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
