using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("Export")]
public sealed class ExportController : BaseController
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private readonly IWordExportService _wordExportService;

    public ExportController(
        IPmTrackerDataStore dataStore,
        IUserContextResolver userContextResolver,
        IWordExportService wordExportService)
        : base(dataStore, userContextResolver)
    {
        _wordExportService = wordExportService;
    }

    [HttpGet("Projekt/{projektId:int}/Tisk")]
    public IActionResult ProjektTisk(int projektId, bool autoPrint = true, string? commentSortDirection = null)
    {
        if (!DataStore.ProjektExists(projektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = DataStore.BuildProjectPrintTemplate(projektId, CurrentUserContext, autoPrint, commentSortDirection);
        return View("~/Views/Export/PdfTemplate.cshtml", model);
    }

    [HttpGet("Projekt/{projektId:int}/Word")]
    public IActionResult ProjektWord(int projektId, string? commentSortDirection = null)
    {
        if (!DataStore.ProjektExists(projektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = DataStore.BuildProjectPrintTemplate(projektId, CurrentUserContext, autoPrint: false, commentSortDirection);
        return BuildWordResult(model);
    }

    [HttpGet("Jednani/{jednaniId:int}/Tisk")]
    public IActionResult JednaniTisk(int jednaniId, bool autoPrint = true, string? commentSortDirection = null)
    {
        var detail = DataStore.BuildJednaniDetail(jednaniId);
        if (!DataStore.ProjektExists(detail.ProjektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.CanAccessProject(detail.ProjektId))
        {
            return NotFound();
        }

        var model = DataStore.BuildMeetingPrintTemplate(jednaniId, CurrentUserContext, autoPrint, commentSortDirection);
        return View("~/Views/Export/PdfTemplate.cshtml", model);
    }

    [HttpGet("Jednani/{jednaniId:int}/Word")]
    public IActionResult JednaniWord(int jednaniId, string? commentSortDirection = null)
    {
        var detail = DataStore.BuildJednaniDetail(jednaniId);
        if (!DataStore.ProjektExists(detail.ProjektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.CanAccessProject(detail.ProjektId))
        {
            return NotFound();
        }

        var model = DataStore.BuildMeetingPrintTemplate(jednaniId, CurrentUserContext, autoPrint: false, commentSortDirection);
        return BuildWordResult(model);
    }

    [HttpGet("Ukol/{zaznamId:int}/Tisk")]
    public IActionResult UkolTisk(int zaznamId, int projektId, bool autoPrint = true, string? commentSortDirection = null)
    {
        if (!DataStore.ProjektExists(projektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = DataStore.BuildTaskPrintTemplate(projektId, zaznamId, CurrentUserContext, autoPrint, commentSortDirection);
        return View("~/Views/Export/PdfTemplate.cshtml", model);
    }

    [HttpGet("Ukol/{zaznamId:int}/Word")]
    public IActionResult UkolWord(int zaznamId, int projektId, string? commentSortDirection = null)
    {
        if (!DataStore.ProjektExists(projektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = DataStore.BuildTaskPrintTemplate(projektId, zaznamId, CurrentUserContext, autoPrint: false, commentSortDirection);
        return BuildWordResult(model);
    }

    // Kompatibilita na staré export URL - přesměrování na nové varianty tisku.
    [HttpGet("Dialog")]
    public IActionResult Dialog(int projektId, int? jednaniId, bool autoPrint = true, string? commentSortDirection = null)
    {
        if (!DataStore.ProjektExists(projektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (jednaniId.HasValue)
        {
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = jednaniId.Value, autoPrint, commentSortDirection });
        }

        return RedirectToAction(nameof(ProjektTisk), new { projektId, autoPrint, commentSortDirection });
    }

    [HttpPost("Pdf")]
    public IActionResult Pdf(PdfExportRequestViewModel request)
    {
        if (request.JednaniId.HasValue)
        {
            return RedirectToAction(nameof(JednaniTisk), new { jednaniId = request.JednaniId.Value, autoPrint = true, commentSortDirection = request.CommentSortDirection });
        }

        return RedirectToAction(nameof(ProjektTisk), new { projektId = request.ProjektId, autoPrint = true, commentSortDirection = request.CommentSortDirection });
    }

    private FileResult BuildWordResult(PdfExportTemplateViewModel model)
    {
        var payload = _wordExportService.BuildDocument(model);
        return File(payload, WordContentType, BuildWordFileName(model));
    }

    private static string BuildWordFileName(PdfExportTemplateViewModel model)
    {
        var projectCode = string.IsNullOrWhiteSpace(model.ProjektZkratka) ? "Projekt" : model.ProjektZkratka.Trim();
        var safeProjectCode = string.Concat(projectCode.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return $"Zapis_{safeProjectCode}_{DateTime.Now:yyyyMMdd_HHmm}.docx";
    }
}
