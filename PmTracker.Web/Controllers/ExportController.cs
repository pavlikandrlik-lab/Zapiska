using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("Export")]
public sealed class ExportController : BaseController
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private readonly IExportTemplateUseCase _exportTemplateUseCase;
    private readonly IProjectService _projectService;
    private readonly IMeetingService _meetingService;
    private readonly IWordExportService _wordExportService;

    public ExportController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IExportTemplateUseCase exportTemplateUseCase,
        IProjectService projectService,
        IMeetingService meetingService,
        IWordExportService wordExportService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _exportTemplateUseCase = exportTemplateUseCase;
        _projectService = projectService;
        _meetingService = meetingService;
        _wordExportService = wordExportService;
    }

    [HttpGet("Projekt/{projektId:int}/Tisk")]
    public async Task<IActionResult> ProjektTisk(int projektId, bool autoPrint = true, CancellationToken ct = default)
    {
        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildProjectTemplateAsync(projektId, CurrentUserContext, autoPrint, ct);
        return View("~/Views/Export/PdfTemplate.cshtml", model);
    }

    [HttpGet("Projekt/{projektId:int}/Word")]
    public async Task<IActionResult> ProjektWord(int projektId, CancellationToken ct = default)
    {
        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildProjectTemplateAsync(projektId, CurrentUserContext, autoPrint: false, ct);
        return BuildWordResult(model);
    }

    [HttpGet("Jednani/{jednaniId:int}/Tisk")]
    public async Task<IActionResult> JednaniTisk(int jednaniId, bool autoPrint = true, CancellationToken ct = default)
    {
        var projectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (!projectId.HasValue)
        {
            return NotFound();
        }

        var accessCheck = await EnsureProjectReadableAsync(projectId.Value, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildMeetingTemplateAsync(jednaniId, CurrentUserContext, autoPrint, ct);
        return View("~/Views/Export/PdfTemplate.cshtml", model);
    }

    [HttpGet("Jednani/{jednaniId:int}/Word")]
    public async Task<IActionResult> JednaniWord(int jednaniId, CancellationToken ct = default)
    {
        var projectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (!projectId.HasValue)
        {
            return NotFound();
        }

        var accessCheck = await EnsureProjectReadableAsync(projectId.Value, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildMeetingTemplateAsync(jednaniId, CurrentUserContext, autoPrint: false, ct);
        return BuildWordResult(model);
    }

    [HttpGet("Ukol/{zaznamId:int}/Tisk")]
    public async Task<IActionResult> UkolTisk(int zaznamId, int projektId, bool autoPrint = true, CancellationToken ct = default)
    {
        var accessCheck = await EnsureProjectReadableAsync(projektId, ct);
        if (accessCheck is not null)
        {
            return accessCheck;
        }

        var model = await _exportTemplateUseCase.BuildTaskTemplateAsync(projektId, zaznamId, CurrentUserContext, autoPrint, ct);
        return View("~/Views/Export/PdfTemplate.cshtml", model);
    }

    [HttpGet("Ukol/{zaznamId:int}/Word")]
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
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = jednaniId.Value, autoPrint });
        }

        return RedirectToAction(nameof(ProjektTisk), new { projektId, autoPrint });
    }

    [HttpPost("Pdf")]
    public IActionResult Pdf(PdfExportRequestViewModel request)
    {
        if (request.JednaniId.HasValue)
        {
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = request.JednaniId.Value, autoPrint = true });
        }

        return RedirectToAction(nameof(ProjektTisk), new { projektId = request.ProjektId, autoPrint = true });
    }

    private FileResult BuildWordResult(PdfExportTemplateViewModel model)
    {
        var payload = _wordExportService.BuildDocument(model);
        return File(payload, WordContentType, BuildWordFileName(model, GetLocalNow()));
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
