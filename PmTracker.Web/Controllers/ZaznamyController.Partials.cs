using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed partial class ZaznamyController
{
    [HttpGet]
    public async Task<IActionResult> RecordCardPartial(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var record = await _recordService.BuildRecordCardShellAsync(projektId, zaznamId, ct);
        if (record is null)
        {
            return NotFound();
        }

        PrepareRecordCardShellPresentation(record, projektId);
        return PartialView("~/Views/Projekty/_ZaznamPartial.cshtml", record);
    }

    [HttpGet]
    public async Task<IActionResult> RecordDetailPartial(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = await _recordService.BuildRecordCardDetailAsync(projektId, zaznamId, ct);
        if (model is null)
        {
            return NotFound();
        }

        return PartialView("~/Views/Projekty/_ZaznamDetailPartial.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> RecordCommentsPartial(int projektId, int zaznamId, int? limit, bool loadAll = false, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var record = await _recordService.BuildRecordCardShellAsync(projektId, zaznamId, ct);
        if (record is null)
        {
            return NotFound();
        }

        var model = await _recordService.BuildRecordCommentsPanelAsync(projektId, zaznamId, limit, loadAll, ct);
        if (model is null)
        {
            return NotFound();
        }

        PrepareRecordCommentsPresentation(model, record.Summary);
        return PartialView("~/Views/Projekty/_ZaznamCommentsPartial.cshtml", model);
    }

    private void PrepareRecordCardShellPresentation(ProjektZaznamCardShellViewModel record, int projektId)
    {
        var summary = record.Summary;
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId);
        var canEditSchedule = summary.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projektId);
        var canAddSchedule = summary.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, projektId);
        var canCommentAsSubsystemLead = CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projektId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);

        summary.CanEditRecord = canEditRecord;
        summary.CanEditSchedule = canEditSchedule;
        summary.CanAddSchedule = canAddSchedule;
        summary.CanManageSchedule = canEditSchedule || canAddSchedule;
        summary.CanCommentAsSubsystemLeader = canCommentAsSubsystemLead;
        summary.CanAddComment = canEditRecord || canCommentAsSubsystemLead;
        summary.EditButtonLabel = canEditRecord ? "Upravit" : "GANTT";
        summary.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        record.DetailUrl ??= Url.Action(nameof(RecordDetailPartial), new { projektId, zaznamId = summary.Id });
        record.CommentsUrl ??= Url.Action(nameof(RecordCommentsPartial), new { projektId, zaznamId = summary.Id });
    }

    private void PrepareRecordCommentsPresentation(ZaznamCommentsPanelViewModel model, ZaznamCardSummaryViewModel summary)
    {
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId);
        var canCommentAsSubsystemLead = CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, model.ProjektId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        if (!canEditRecord && canCommentAsSubsystemLead)
        {
            model.OtevrenaJednani = model.OtevrenaJednani
                .Where(item => item.IsDraft)
                .ToList();
        }

        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CanEditRecord = canEditRecord;
        model.CanCommentAsSubsystemLeader = canCommentAsSubsystemLead;
        model.CanAddComment = model.OtevrenaJednani.Count > 0 && (canEditRecord || canCommentAsSubsystemLead);
    }
}
