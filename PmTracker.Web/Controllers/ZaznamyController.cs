using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ZaznamyController : BaseController
{
    private readonly IRecordsService _recordsService;

    public ZaznamyController(
        IPmTrackerDataStore dataStore,
        IUserContextResolver userContextResolver,
        IRecordsService recordsService)
        : base(dataStore, userContextResolver)
    {
        _recordsService = recordsService;
    }

    public IActionResult Edit(int id)
    {
        var model = _recordsService.BuildZaznamEdit(id);
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId);
        var canManageSchedule = model.JeUkolKategorie
            && (CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, model.ProjektId)
                || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, model.ProjektId));
        if (!canEditRecord && !canManageSchedule)
        {
            return Forbid();
        }

        return View("~/Views/Projekty/EditZaznam.cshtml", model);
    }

    public IActionResult Create(int projektId)
    {
        if (!_recordsService.ProjektExists(projektId))
        {
            return RedirectToAction("Index", "Projekty");
        }

        var model = _recordsService.BuildZaznamCreate(projektId);
        return View("~/Views/Projekty/EditZaznam.cshtml", model);
    }

    [HttpGet]
    public IActionResult AssignMeetingIdentifierModal(int projektId, int zaznamId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId))
        {
            return Forbid();
        }

        var model = _recordsService.BuildZaznamEdit(zaznamId);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Save(SaveRecordCommand command)
    {
        var uiTab = string.Equals(command.UiTab, "harmonogram", StringComparison.OrdinalIgnoreCase)
            ? "harmonogram"
            : "zaznamy";
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId);
        var canEditSchedule = CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, command.ProjektId);
        var canAddSchedule = CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, command.ProjektId);
        var canSaveScheduleOnly = command.Id.HasValue
            && string.Equals(uiTab, "harmonogram", StringComparison.OrdinalIgnoreCase)
            && (canEditSchedule || canAddSchedule);
        var redirectToProject = () => RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "zaznamy" });
        var savedRecordId = 0;

        return ExecuteValidatedCommand(
            hasPermission: () => canEditRecord || canSaveScheduleOnly,
            invalidAjaxMessage: "Záznam nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: redirectToProject,
            onSuccessRedirect: redirectToProject,
            onAjaxSuccess: () =>
            {
                if (command.Id.HasValue)
                {
                    return AjaxSuccessResult(
                        refreshScope: "record-card-with-schedules",
                        refreshUrl: Url.Action(nameof(RecordCardPartial), new { projektId = command.ProjektId, zaznamId = savedRecordId }),
                        projectId: command.ProjektId,
                        recordId: savedRecordId,
                        uiContext: "project",
                        tab: uiTab,
                        message: "Záznam byl uložen.");
                }

                return AjaxSuccessResult(
                    refreshScope: "projekty-detail-zaznamy-preserve",
                    refreshUrl: Url.Action("Detail", "Projekty", new { id = command.ProjektId, tab = uiTab }),
                    projectId: command.ProjektId,
                    tab: uiTab,
                    message: "Záznam byl uložen.");
            },
            operation: () => savedRecordId = _recordsService.SaveRecord(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignMeetingIdentifier(AssignMeetingIdentifierCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId),
            invalidAjaxMessage: "Identifikátor z jednání nelze doplnit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "zaznamy" }),
            onSuccessRedirect: () => RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "zaznamy" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "record-card",
                refreshUrl: Url.Action(nameof(RecordCardPartial), new { projektId = command.ProjektId, zaznamId = command.ZaznamId }),
                projectId: command.ProjektId,
                recordId: command.ZaznamId,
                uiContext: "project",
                tab: "zaznamy",
                message: "Identifikátor z jednání byl doplněn."),
            operation: () => _recordsService.AssignMeetingIdentifier(command, CurrentUserContext));
    }

    [HttpGet]
    public IActionResult RecordCardPartial(int projektId, int zaznamId)
    {
        var model = _recordsService.BuildProjektDetail(projektId);
        var record = model.Zaznamy.FirstOrDefault(x => x.Id == zaznamId);
        if (record is null)
        {
            return NotFound();
        }

        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId);
        var canEditSchedule = record.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projektId);
        var canAddSchedule = record.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, projektId);
        var canCommentAsSubsystemLead = CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projektId)
            && CurrentUserContext.OsobaId == record.AktualniSubsystemVedouciOsobaId;
        if (!canEditRecord && !canCommentAsSubsystemLead && !canEditSchedule && !canAddSchedule)
        {
            return Forbid();
        }

        ViewData["OtevrenaJednani"] = model.OtevrenaJednani;
        ViewData["ProjektId"] = projektId;
        return PartialView("~/Views/Projekty/_ZaznamPartial.cshtml", record);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddComment(int projektId, AddCommentCommand command, string? uiContext, int? meetingId, string? returnUrl)
    {
        var redirect = () => RedirectToContextOrDefault(uiContext, projektId, meetingId ?? command.JednaniId, returnUrl);
        return ExecuteValidatedCommand(
            hasPermission: () => true,
            invalidAjaxMessage: "Vyjádření nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: redirect,
            onSuccessRedirect: redirect,
            onAjaxSuccess: () => BuildCommentAjaxSuccessResult(uiContext, projektId, command.ZaznamId, meetingId ?? command.JednaniId, "Vyjádření bylo uloženo."),
            operation: () => _recordsService.AddComment(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateComment(int projektId, UpdateCommentCommand command, int? zaznamId, string? uiContext, int? meetingId, string? returnUrl)
    {
        var redirect = () => RedirectToContextOrDefault(uiContext, projektId, meetingId, returnUrl);
        return ExecuteValidatedCommand(
            hasPermission: () => true,
            invalidAjaxMessage: "Vyjádření nelze upravit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: redirect,
            onSuccessRedirect: redirect,
            onAjaxSuccess: () =>
            {
                if (!zaznamId.HasValue || zaznamId.Value <= 0)
                {
                    return AjaxErrorResult("Chybí ID záznamu pro obnovu vyjádření.");
                }

                return BuildCommentAjaxSuccessResult(uiContext, projektId, zaznamId.Value, meetingId, "Vyjádření bylo upraveno.");
            },
            operation: () => _recordsService.UpdateComment(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteComment(int projektId, DeleteCommentCommand command, int? zaznamId, string? uiContext, int? meetingId, string? returnUrl)
    {
        var redirect = () => RedirectToContextOrDefault(uiContext, projektId, meetingId, returnUrl);
        return ExecuteCommand(
            hasPermission: () => true,
            onSuccessRedirect: redirect,
            onAjaxSuccess: () =>
            {
                if (!zaznamId.HasValue || zaznamId.Value <= 0)
                {
                    return AjaxErrorResult("Chybí ID záznamu pro obnovu vyjádření.");
                }

                return BuildCommentAjaxSuccessResult(uiContext, projektId, zaznamId.Value, meetingId, "Vyjádření bylo smazáno.");
            },
            operation: () => _recordsService.DeleteComment(command, CurrentUserContext));
    }

    private IActionResult BuildCommentAjaxSuccessResult(string? uiContext, int projektId, int zaznamId, int? meetingId, string message)
    {
        if (string.Equals(uiContext, "meeting", StringComparison.OrdinalIgnoreCase) && meetingId.HasValue)
        {
            return AjaxSuccessResult(
                refreshScope: "meeting-task-item",
                refreshUrl: Url.Action("TaskItemPartial", "Jednani", new { jednaniId = meetingId.Value, zaznamId }),
                projectId: projektId,
                recordId: zaznamId,
                meetingId: meetingId,
                uiContext: "meeting",
                message: message);
        }

        return AjaxSuccessResult(
            refreshScope: "record-card",
            refreshUrl: Url.Action(nameof(RecordCardPartial), new { projektId, zaznamId }),
            projectId: projektId,
            recordId: zaznamId,
            uiContext: "project",
            tab: "zaznamy",
            message: message);
    }

    private IActionResult RedirectToContextOrDefault(string? uiContext, int projektId, int? meetingId, string? returnUrl)
    {
        if (string.Equals(uiContext, "meeting", StringComparison.OrdinalIgnoreCase) && meetingId.HasValue)
        {
            return RedirectToAction("Detail", "Jednani", new { id = meetingId.Value });
        }

        if (string.Equals(uiContext, "project", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction("Detail", "Projekty", new { id = projektId, tab = "zaznamy" });
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Detail", "Projekty", new { id = projektId, tab = "zaznamy" });
    }
}
