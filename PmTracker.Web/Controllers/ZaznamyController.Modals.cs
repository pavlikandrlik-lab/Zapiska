using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed partial class ZaznamyController
{
    [HttpGet]
    public async Task<IActionResult> DeleteRecordModal(int projektId, int zaznamId, string? returnUrl, string? uiContext, string? tab, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId))
        {
            return Forbid();
        }

        var model = await _recordService.BuildDeleteRecordModalAsync(projektId, zaznamId, ct);
        ViewData["DeleteRecordReturnUrl"] = NormalizeLocalReturnUrl(returnUrl);
        ViewData["DeleteRecordUiContext"] = string.Equals(uiContext, "page", StringComparison.OrdinalIgnoreCase) ? "page" : "project";
        ViewData["DeleteRecordTab"] = NormalizeDeleteTab(tab);
        return View("~/Views/Projekty/DeleteRecordModal.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> AssignMeetingIdentifierModal(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId))
        {
            return Forbid();
        }

        var model = await _recordService.BuildZaznamEditAsync(zaznamId, ct);
        if (model.ProjektId != projektId)
        {
            return NotFound();
        }

        var modal = new AssignMeetingIdentifierModalViewModel
        {
            Title = "Doplnit identifikátor z jednání",
            Command = new AssignMeetingIdentifierCommand
            {
                ProjektId = projektId,
                ZaznamId = zaznamId
            },
            JednaniOptions = model.JednaniProCisloOptions,
            CisloViditelne = model.CisloViditelne
        };

        return View("~/Views/Projekty/AssignMeetingIdentifierModal.cshtml", modal);
    }
}
