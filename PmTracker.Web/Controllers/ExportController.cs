using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;

namespace PmTracker.Web.Controllers;

[Route("Export")]
[Authorize]
public sealed class ExportController : BaseController
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfContentType = "application/pdf";
    private const string PdfViewPath = "~/Views/Export/PdfTemplate.cshtml";
    private readonly IExportTemplateUseCase _exportTemplateUseCase;
    private readonly IProjectService _projectService;
    private readonly IMeetingService _meetingService;
    private readonly IWordExportService _wordExportService;
    private readonly IVyzvaExportBuilder _vyzvaExportBuilder;
    private readonly IVyzvaWordExportService _vyzvaWordExportService;
    private readonly IPdfRenderer _pdfRenderer;
    private readonly IViewRenderer _viewRenderer;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ExportController> _logger;

    public ExportController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IExportTemplateUseCase exportTemplateUseCase,
        IProjectService projectService,
        IMeetingService meetingService,
        IWordExportService wordExportService,
        IVyzvaExportBuilder vyzvaExportBuilder,
        IVyzvaWordExportService vyzvaWordExportService,
        IPdfRenderer pdfRenderer,
        IViewRenderer viewRenderer,
        IWebHostEnvironment environment)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _exportTemplateUseCase = exportTemplateUseCase;
        _projectService = projectService;
        _meetingService = meetingService;
        _wordExportService = wordExportService;
        _vyzvaExportBuilder = vyzvaExportBuilder;
        _vyzvaWordExportService = vyzvaWordExportService;
        _pdfRenderer = pdfRenderer;
        _viewRenderer = viewRenderer;
        _environment = environment;
        _logger = loggerFactory.CreateLogger<ExportController>();
    }

    [HttpGet("Projekt/{projektId:int}/Tisk")]
    [Authorize(Policy = "permission:export.pdf.projekt")]
    public async Task<IActionResult> ProjektTisk(
        int projektId,
        bool autoPrint = true,
        bool useCurrentFilters = false,
        string? subsystem = null,
        string? kategorie = null,
        string? stav = null,
        string? typ = null,
        int? vlastnik = null,
        bool aktivni = false,
        int? jednaniVyjadreniStav = null,
        CancellationToken ct = default)
    {
        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var filters = BuildProjectExportFilters(
            useCurrentFilters,
            subsystem,
            kategorie,
            stav,
            typ,
            vlastnik,
            aktivni,
            jednaniVyjadreniStav);

        var model = await _exportTemplateUseCase.BuildProjectTemplateAsync(projektId, CurrentUserContext, autoPrint, filters, ct);
        return await BuildPrintResultAsync(model, ct);
    }

    [HttpGet("Projekt/{projektId:int}/Word")]
    [Authorize(Policy = "permission:export.word.projekt")]
    public async Task<IActionResult> ProjektWord(
        int projektId,
        bool useCurrentFilters = false,
        string? subsystem = null,
        string? kategorie = null,
        string? stav = null,
        string? typ = null,
        int? vlastnik = null,
        bool aktivni = false,
        int? jednaniVyjadreniStav = null,
        CancellationToken ct = default)
    {
        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var filters = BuildProjectExportFilters(
            useCurrentFilters,
            subsystem,
            kategorie,
            stav,
            typ,
            vlastnik,
            aktivni,
            jednaniVyjadreniStav);

        var model = await _exportTemplateUseCase.BuildProjectTemplateAsync(projektId, CurrentUserContext, autoPrint: false, filters, ct);
        return BuildWordResult(model);
    }

    [HttpGet("Jednani/{jednaniId:int}/Tisk")]
    [Authorize(Policy = "permission:export.pdf.jednani")]
    public async Task<IActionResult> JednaniTisk(int jednaniId, int projektId, bool autoPrint = true, CancellationToken ct = default)
    {
        var meetingProjectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (meetingProjectId is null)
        {
            return NotFound();
        }

        // projektId nese authz kontext pro project-scoped policy. Je-li zadané, musí odpovídat
        // skutečnému projektu jednání (jinak by šlo autorizovat proti spravovanému projektu a
        // tisknout cizí jednání). Bez projektId projde jen globálně oprávněný uživatel (policy
        // udělá global check ještě před tělem) — zpětná kompatibilita přímých URL.
        if (projektId != 0 && projektId != meetingProjectId.Value)
        {
            return NotFound();
        }

        var accessCheck = await EnsureProjectReadableAsync(meetingProjectId.Value, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildMeetingTemplateAsync(jednaniId, CurrentUserContext, autoPrint, ct);
        return await BuildPrintResultAsync(model, ct);
    }

    [HttpGet("Jednani/{jednaniId:int}/Word")]
    [Authorize(Policy = "permission:export.word.jednani")]
    public async Task<IActionResult> JednaniWord(int jednaniId, int projektId, CancellationToken ct = default)
    {
        var meetingProjectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (meetingProjectId is null)
        {
            return NotFound();
        }

        if (projektId != 0 && projektId != meetingProjectId.Value)
        {
            return NotFound();
        }

        var accessCheck = await EnsureProjectReadableAsync(meetingProjectId.Value, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildMeetingTemplateAsync(jednaniId, CurrentUserContext, autoPrint: false, ct);
        return BuildWordResult(model);
    }

    [HttpGet("Ukol/{zaznamId:int}/Tisk")]
    [Authorize(Policy = "permission:export.pdf.ukol")]
    public async Task<IActionResult> UkolTisk(int zaznamId, int projektId, bool autoPrint = true, CancellationToken ct = default)
    {
        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildTaskTemplateAsync(projektId, zaznamId, CurrentUserContext, autoPrint, ct);
        return await BuildPrintResultAsync(model, ct);
    }

    [HttpGet("Ukol/{zaznamId:int}/Word")]
    [Authorize(Policy = "permission:export.word.ukol")]
    public async Task<IActionResult> UkolWord(int zaznamId, int projektId, CancellationToken ct = default)
    {
        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildTaskTemplateAsync(projektId, zaznamId, CurrentUserContext, autoPrint: false, ct);
        return BuildWordResult(model);
    }

    // Kompatibilita na staré export URL - přesměrování na nové varianty tisku.
    [HttpGet("Dialog")]
    public async Task<IActionResult> Dialog(int projektId, int? jednaniId, bool autoPrint = true, CancellationToken ct = default)
    {
        if (!await _projectService.ProjektExistsAsync(projektId, ct))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (jednaniId.HasValue)
        {
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = jednaniId.Value, projektId, autoPrint });
        }

        return RedirectToAction(nameof(ProjektTisk), new { projektId, autoPrint });
    }

    [HttpPost("Pdf")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:export.pdf.projekt")]
    public IActionResult Pdf(PdfExportRequestViewModel request)
    {
        if (request.JednaniId.HasValue)
        {
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = request.JednaniId.Value, projektId = request.ProjektId, autoPrint = true });
        }

        return RedirectToAction(nameof(ProjektTisk), new { projektId = request.ProjektId, autoPrint = true });
    }

    /// <summary>
    /// Serverové PDF (2026-09-04): vyrenderuje tiskovou šablonu, nechá ji vysázet
    /// prohlížečem a vrátí dokument k otevření. Když sazba selže, vrátí se dnešní
    /// HTML tisk — tisk tak nikdy nepřestane fungovat (spec §8).
    /// </summary>
    /// <summary>
    /// Výzva k poskytnutí plnění podle resortního formuláře (spec 2026-09-07 §9). PDF je převod
    /// Wordu výzvy (uživatel 2026-10-07: „dej do stejného formátu i PDF“) — obsah i formát má
    /// jediný zdroj, PDF se od Wordu a tím od vzoru nemůže rozejít.
    /// </summary>
    [HttpGet("Vyzva/{vyzvaId:int}/Tisk")]
    [Authorize(Policy = "permission:vyzvy.word.export")]
    public async Task<IActionResult> VyzvaTisk(int vyzvaId, int projektId, CancellationToken ct = default)
    {
        var model = await LoadVyzvaExportAsync(vyzvaId, projektId, ct);
        if (model is null) return NotFound();

        var accessCheck = await EnsureProjectReadableAsync(model.ProjektId, ct);
        if (accessCheck is not null) return accessCheck;

        var dokument = WordNaHtml.Preved(
            _vyzvaWordExportService.BuildDocument(model), $"Výzva č. {model.KodVyzvy}");

        var result = await _pdfRenderer.RenderAsync(new PdfRenderRequest
        {
            Html = dokument.Html,
            HeaderTemplate = dokument.ZahlaviSablona,
            FooterTemplate = dokument.ZapatiSablona,
            PreferCssPageSize = true,
        }, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "PDF výzvy se nevyrobilo ({Duvod}), tisk pokračuje HTML cestou.", result.FailureReason);
            return Content(dokument.Html, "text/html; charset=utf-8");
        }

        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = VyzvaExportFileName(model, "pdf")
        }.ToString();

        return File(result.Bytes!, PdfContentType);
    }

    [HttpGet("Vyzva/{vyzvaId:int}/Word")]
    [Authorize(Policy = "permission:vyzvy.word.export")]
    public async Task<IActionResult> VyzvaWord(int vyzvaId, int projektId, CancellationToken ct = default)
    {
        var model = await LoadVyzvaExportAsync(vyzvaId, projektId, ct);
        if (model is null) return NotFound();

        var accessCheck = await EnsureProjectReadableAsync(model.ProjektId, ct);
        if (accessCheck is not null) return accessCheck;

        var payload = _vyzvaWordExportService.BuildDocument(model);
        return File(payload, WordContentType, VyzvaExportFileName(model, "docx"));
    }

    /// <summary>
    /// Null, když výzva neexistuje nebo zadané projektId neodpovídá jejímu projektu.
    /// Bez té kontroly by šlo autorizovat proti spravovanému projektu a tisknout cizí výzvu
    /// (stejný guard jako u jednání).
    /// </summary>
    private async Task<Models.ViewModels.Vyzvy.VyzvaExportViewModel?> LoadVyzvaExportAsync(
        int vyzvaId, int projektId, CancellationToken ct)
    {
        var model = await _vyzvaExportBuilder.BuildAsync(vyzvaId, ct);
        if (model is null) return null;
        if (projektId != 0 && projektId != model.ProjektId) return null;
        return model;
    }

    private static string VyzvaExportFileName(
        Models.ViewModels.Vyzvy.VyzvaExportViewModel model, string pripona)
        => $"Vyzva_{model.PoradoveVRoce}_{model.Rok}_{model.InformacniSystem}.{pripona}";

    private async Task<IActionResult> BuildPrintResultAsync(
        PdfExportTemplateViewModel model, CancellationToken ct)
    {
        var html = await _viewRenderer.RenderToStringAsync(ControllerContext, PdfViewPath, model);
        var stylesheetPath = Path.Combine(_environment.WebRootPath, "css", "pdf-export.css");

        var result = await _pdfRenderer.RenderAsync(
            new PdfRenderRequest { Html = html, StylesheetPath = stylesheetPath }, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "PDF se nevyrobilo ({Duvod}), tisk pokračuje HTML cestou.", result.FailureReason);
            return View(PdfViewPath, model);
        }

        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
        {
            FileNameStar = PdfExportFileName.Build(model, GetLocalNow())
        }.ToString();

        return File(result.Bytes!, PdfContentType);
    }

    private FileResult BuildWordResult(PdfExportTemplateViewModel model)
    {
        var payload = _wordExportService.BuildDocument(model);
        return File(payload, WordContentType, BuildWordFileName(model, GetLocalNow()));
    }

    private static ProjectExportRecordFilters BuildProjectExportFilters(
        bool useCurrentFilters,
        string? subsystem,
        string? kategorie,
        string? stav,
        string? typ,
        int? vlastnik,
        bool aktivni,
        int? jednaniVyjadreniStav)
    {
        return new ProjectExportRecordFilters
        {
            UseCurrentFilters = useCurrentFilters,
            Subsystem = string.IsNullOrWhiteSpace(subsystem) ? null : subsystem.Trim(),
            Kategorie = string.IsNullOrWhiteSpace(kategorie) ? null : kategorie.Trim(),
            Stav = string.IsNullOrWhiteSpace(stav) ? null : stav.Trim(),
            Typ = string.IsNullOrWhiteSpace(typ) ? null : typ.Trim(),
            VlastnikId = vlastnik > 0 ? vlastnik : null,
            Aktivni = aktivni,
            JednaniVyjadreniStavId = jednaniVyjadreniStav > 0 ? jednaniVyjadreniStav : null
        };
    }

    private async Task<IActionResult?> EnsureProjectReadableAsync(int projektId, CancellationToken ct)
    {
        if (!await _projectService.ProjektExistsAsync(projektId, ct))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        return null;
    }

    private static string BuildWordFileName(PdfExportTemplateViewModel model, DateTime localNow)
    {
        var projectCode = string.IsNullOrWhiteSpace(model.ProjektZkratka) ? "Projekt" : model.ProjektZkratka.Trim();
        var safeProjectCode = string.Concat(projectCode.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return $"Zapis_{safeProjectCode}_{localNow:yyyyMMdd_HHmm}.docx";
    }
}
