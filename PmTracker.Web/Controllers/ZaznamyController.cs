using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ZaznamyController : BaseController
{
    private const string PresentationModal = "modal";
    private const string PresentationPage = "page";
    private const string EditorTabBasic = "basic";
    private const string EditorTabExternal = "external";
    private const string EditorTabCollaboration = "collaboration";
    private const string EditorTabSchedule = "schedule";
    private const string UiContextProject = "project";
    private const string UiContextMeeting = "meeting";

    private readonly IRecordService _recordService;
    private readonly IRecordUiFlowResolver _recordUiFlowResolver;

    public ZaznamyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IRecordService recordService,
        IRecordUiFlowResolver recordUiFlowResolver)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _recordService = recordService;
        _recordUiFlowResolver = recordUiFlowResolver;
    }

    public async Task<IActionResult> Edit(int id, string? presentation, string? returnUrl, CancellationToken ct = default)
    {
        var model = await _recordService.BuildZaznamEditAsync(id, ct);
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId);
        var canManageSchedule = model.JeUkolKategorie
            && (CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, model.ProjektId)
                || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, model.ProjektId));
        if (!canEditRecord && !canManageSchedule)
        {
            return Forbid();
        }

        PrepareRecordEditorModel(model, presentation, returnUrl, canEditRecord, canManageSchedule);
        return View(GetEditorViewPath(model.Presentation), model);
    }

    public async Task<IActionResult> Create(int projektId, int? jednaniId, string? uiContext, string? presentation, string? returnUrl, CancellationToken ct = default)
    {
        if (!await _recordService.ProjektExistsAsync(projektId, ct))
        {
            return RedirectToAction("Index", "Projekty");
        }

        if (!CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId))
        {
            return Forbid();
        }

        var normalizedUiContext = NormalizeRecordEditorUiContext(uiContext, jednaniId);
        var contextMeetingId = string.Equals(normalizedUiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase)
            ? jednaniId
            : null;
        var model = await _recordService.BuildZaznamCreateAsync(projektId, contextMeetingId, ct);
        PrepareRecordEditorModel(
            model,
            presentation,
            returnUrl,
            canEditRecord: true,
            canManageSchedule: model.JeUkolKategorie,
            uiContext: normalizedUiContext,
            meetingId: contextMeetingId);
        return View(GetEditorViewPath(model.Presentation), model);
    }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SaveRecordCommand command, CancellationToken ct = default)
    {
        var editorTab = NormalizeEditorTab(command.EditorTab);
        var projectTab = NormalizeProjectTab(editorTab);
        var presentation = NormalizePresentation(command.Presentation);
        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId);
        var canEditSchedule = CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, command.ProjektId);
        var canAddSchedule = CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, command.ProjektId);
        var canSaveScheduleOnly = command.Id.HasValue
            && string.Equals(editorTab, EditorTabSchedule, StringComparison.OrdinalIgnoreCase)
            && (canEditSchedule || canAddSchedule);
        var normalizedUiContext = NormalizeRecordEditorUiContext(command.UiContext, command.MeetingId);
        var contextMeetingId = string.Equals(normalizedUiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase)
            ? command.MeetingId
            : null;
        var redirectAfterSave = () => RedirectRecordEditorSaveTarget(command.ProjektId, command.ReturnUrl, projectTab, normalizedUiContext, contextMeetingId);
        var savedRecordId = 0;

        return await ExecuteValidatedCommandAsync(
            hasPermission: () => canEditRecord || canSaveScheduleOnly,
            invalidAjaxMessage: "Záznam nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: redirectAfterSave,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(redirectAfterSave()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildSaveAjaxSuccessResult(
                command,
                savedRecordId,
                projectTab,
                presentation,
                normalizedUiContext,
                contextMeetingId)),
            operation: async () => savedRecordId = await _recordService.SaveRecordAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRecord(DeleteRecordCommand command, string? returnUrl, string? uiContext, string? tab, CancellationToken ct = default)
    {
        var normalizedTab = NormalizeDeleteTab(tab);
        var redirectUrl = BuildDeleteRecordReturnUrl(command.ProjektId, returnUrl, uiContext, normalizedTab);
        var redirect = () => Redirect(redirectUrl);

        return await ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId),
            invalidAjaxMessage: "Záznam nelze smazat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: redirect,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(redirect()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildDeleteRecordAjaxSuccessResult(command.ProjektId, redirectUrl, returnUrl, uiContext, normalizedTab)),
            operation: () => _recordService.DeleteRecordAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CancellationToken ct = default)
    {
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId),
            invalidAjaxMessage: "Identifikátor z jednání nelze doplnit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "zaznamy" }),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "zaznamy" })!),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "record-card",
                refreshUrl: Url.Action(nameof(RecordCardPartial), new { projektId = command.ProjektId, zaznamId = command.ZaznamId }),
                projectId: command.ProjektId,
                recordId: command.ZaznamId,
                uiContext: "project",
                tab: "zaznamy",
                message: "Identifikátor z jednání byl doplněn.")),
            operation: () => _recordService.AssignMeetingIdentifierAsync(command, CurrentUserContext, ct));
    }

    [HttpGet]
    public async Task<IActionResult> RecordCardPartial(int projektId, int zaznamId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = await _recordService.BuildProjektDetailAsync(projektId, ct);
        var record = model.Zaznamy.FirstOrDefault(x => x.Id == zaznamId);
        if (record is null)
        {
            return NotFound();
        }

        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId);
        var canEditSchedule = record.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projektId);
        var canAddSchedule = record.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, projektId);
        var canCommentAsSubsystemLead = CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projektId)
            && record.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        if (!canEditRecord && !canCommentAsSubsystemLead && !canEditSchedule && !canAddSchedule)
        {
            return Forbid();
        }

        record.CanEditRecord = canEditRecord;
        record.CanEditSchedule = canEditSchedule;
        record.CanAddSchedule = canAddSchedule;
        record.CanManageSchedule = canEditSchedule || canAddSchedule;
        record.CanCommentAsSubsystemLeader = canCommentAsSubsystemLead;
        record.CanAddComment = canEditRecord || canCommentAsSubsystemLead;
        record.EditButtonLabel = canEditRecord ? "Upravit" : "GANTT";
        record.CurrentUserOsobaId = CurrentUserContext.OsobaId;

        ViewData["OtevrenaJednani"] = model.OtevrenaJednani;
        ViewData["ProjektId"] = projektId;
        return PartialView("~/Views/Projekty/_ZaznamPartial.cshtml", record);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int projektId, AddCommentCommand command, string? uiContext, int? meetingId, string? returnUrl, CancellationToken ct = default)
    {
        var redirect = () => ResolveCommentRedirect(uiContext, projektId, meetingId ?? command.JednaniId, returnUrl);
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => true,
            invalidAjaxMessage: "Vyjádření nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: redirect,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(redirect()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildCommentAjaxSuccessResult(uiContext, projektId, command.ZaznamId, meetingId ?? command.JednaniId, "Vyjádření bylo uloženo.")),
            operation: () => _recordService.AddCommentAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateComment(int projektId, UpdateCommentCommand command, int? zaznamId, string? uiContext, int? meetingId, string? returnUrl, CancellationToken ct = default)
    {
        var redirect = () => ResolveCommentRedirect(uiContext, projektId, meetingId, returnUrl);
        return await ExecuteValidatedCommandAsync(
            hasPermission: () => true,
            invalidAjaxMessage: "Vyjádření nelze upravit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: redirect,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(redirect()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(
            (
                !zaznamId.HasValue || zaznamId.Value <= 0
                    ? AjaxErrorResult("Chybí ID záznamu pro obnovu vyjádření.")
                    : BuildCommentAjaxSuccessResult(uiContext, projektId, zaznamId.Value, meetingId, "Vyjádření bylo upraveno.")
            )),
            operation: () => _recordService.UpdateCommentAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int projektId, DeleteCommentCommand command, int? zaznamId, string? uiContext, int? meetingId, string? returnUrl, CancellationToken ct = default)
    {
        var redirect = () => ResolveCommentRedirect(uiContext, projektId, meetingId, returnUrl);
        return await ExecuteCommandAsync(
            hasPermission: () => true,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(redirect()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(
                !zaznamId.HasValue || zaznamId.Value <= 0
                    ? AjaxErrorResult("Chybí ID záznamu pro obnovu vyjádření.")
                    : BuildCommentAjaxSuccessResult(uiContext, projektId, zaznamId.Value, meetingId, "Vyjádření bylo smazáno.")),
            operation: () => _recordService.DeleteCommentAsync(command, CurrentUserContext, ct));
    }

    private IActionResult BuildCommentAjaxSuccessResult(string? uiContext, int projektId, int zaznamId, int? meetingId, string message)
    {
        var flow = _recordUiFlowResolver.ResolveCommentAjaxFlow(uiContext, projektId, zaznamId, meetingId);
        return AjaxSuccessResult(
            refreshScope: flow.RefreshScope,
            refreshUrl: Url.Action(flow.ActionName, flow.ControllerName, flow.RouteValues),
            projectId: projektId,
            recordId: zaznamId,
            meetingId: flow.MeetingId,
            uiContext: flow.UiContext,
            tab: flow.Tab,
            message: message);
    }

    private IActionResult BuildSaveAjaxSuccessResult(
        SaveRecordCommand command,
        int savedRecordId,
        string projectTab,
        string presentation,
        string normalizedUiContext,
        int? contextMeetingId)
    {
        var meetingDetailUrl = contextMeetingId.HasValue
            ? BuildMeetingDetailUrl(contextMeetingId.Value)
            : null;

        if (string.Equals(presentation, PresentationPage, StringComparison.OrdinalIgnoreCase))
        {
            var refreshUrl = string.Equals(normalizedUiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(meetingDetailUrl)
                ? meetingDetailUrl
                : BuildRestoreReturnUrl(command.ProjektId, command.ReturnUrl, projectTab);
            return AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: refreshUrl,
                projectId: command.ProjektId,
                recordId: savedRecordId,
                meetingId: contextMeetingId,
                uiContext: normalizedUiContext,
                tab: projectTab,
                message: "Záznam byl uložen.");
        }

        if (command.Id.HasValue)
        {
            return AjaxSuccessResult(
                refreshScope: "record-card-with-schedules",
                refreshUrl: Url.Action(nameof(RecordCardPartial), new { projektId = command.ProjektId, zaznamId = savedRecordId }),
                projectId: command.ProjektId,
                recordId: savedRecordId,
                uiContext: UiContextProject,
                tab: projectTab,
                message: "Záznam byl uložen.");
        }

        if (string.Equals(normalizedUiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(meetingDetailUrl))
        {
            return AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: meetingDetailUrl,
                projectId: command.ProjektId,
                recordId: savedRecordId,
                meetingId: contextMeetingId,
                uiContext: UiContextMeeting,
                message: "Záznam byl uložen.");
        }

        return AjaxSuccessResult(
            refreshScope: "projekty-detail-zaznamy-preserve",
            refreshUrl: Url.Action("Detail", "Projekty", new { id = command.ProjektId, tab = projectTab }),
            projectId: command.ProjektId,
            uiContext: UiContextProject,
            tab: projectTab,
            message: "Záznam byl uložen.");
    }

    private IActionResult BuildDeleteRecordAjaxSuccessResult(int projektId, string redirectUrl, string? returnUrl, string? uiContext, string normalizedTab)
    {
        if (string.Equals(uiContext, "page", StringComparison.OrdinalIgnoreCase))
        {
            return AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: redirectUrl,
                projectId: projektId,
                uiContext: UiContextProject,
                tab: normalizedTab,
                message: "Záznam byl smazán.");
        }

        return AjaxSuccessResult(
            refreshScope: "projekty-detail-zaznamy-preserve",
            refreshUrl: NormalizeLocalReturnUrl(returnUrl)
                ?? (Url.Action("Detail", "Projekty", new { id = projektId, tab = normalizedTab }) ?? $"/Projekty/Detail/{projektId}?tab={normalizedTab}"),
            projectId: projektId,
            uiContext: UiContextProject,
            tab: normalizedTab,
            message: "Záznam byl smazán.");
    }

    private IActionResult ResolveCommentRedirect(string? uiContext, int projektId, int? meetingId, string? returnUrl)
    {
        var flow = _recordUiFlowResolver.ResolveCommentRedirectFlow(
            uiContext,
            projektId,
            meetingId,
            NormalizeLocalReturnUrl(returnUrl));
        if (flow.IsLocalRedirect)
        {
            return Redirect(flow.LocalUrl!);
        }

        return RedirectToAction(flow.ActionName, flow.ControllerName, flow.RouteValues)!;
    }

    private void PrepareRecordEditorModel(
        ZaznamEditViewModel model,
        string? requestedPresentation,
        string? requestedReturnUrl,
        bool canEditRecord,
        bool canManageSchedule,
        string? uiContext = null,
        int? meetingId = null)
    {
        var normalizedUiContext = NormalizeRecordEditorUiContext(uiContext, meetingId);
        var normalizedMeetingId = string.Equals(normalizedUiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase)
            ? meetingId
            : null;
        model.Presentation = ResolvePresentation(requestedPresentation);
        model.ReturnUrl = NormalizeLocalReturnUrl(requestedReturnUrl);
        model.UiContext = normalizedUiContext;
        model.MeetingId = normalizedMeetingId;
        var fallbackBackUrl = normalizedMeetingId.HasValue
            ? BuildMeetingDetailUrl(normalizedMeetingId.Value)
            : (Url.Action("Detail", "Projekty", new { id = model.ProjektId, tab = "zaznamy" }) ?? $"/Projekty/Detail/{model.ProjektId}?tab=zaznamy");
        model.BackUrl = NormalizeLocalReturnUrl(requestedReturnUrl)
            ?? fallbackBackUrl;
        model.PageTitle = model.IsCreate ? "Nový projektový záznam" : "Upravit záznam";
        model.BackLabel = normalizedMeetingId.HasValue ? "Zpět na jednání" : "Zpět do projektu";
        model.CanEditRecord = canEditRecord;
        model.CanEditScheduleFull = canEditRecord || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, model.ProjektId);
        model.CanEditScheduleAddOnly = !model.CanEditScheduleFull
            && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, model.ProjektId);
        model.ActiveEditorTab = !canEditRecord && canManageSchedule && model.JeUkolKategorie
            ? EditorTabSchedule
            : EditorTabBasic;
        model.UseAjaxSubmit = true;
    }

    private string GetEditorViewPath(string presentation)
        => string.Equals(presentation, PresentationPage, StringComparison.OrdinalIgnoreCase)
            ? "~/Views/Projekty/EditZaznamPage.cshtml"
            : "~/Views/Projekty/EditZaznamModal.cshtml";

    private string ResolvePresentation(string? requestedPresentation)
    {
        var normalized = NormalizePresentation(requestedPresentation);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return IsAjaxRequest() ? PresentationModal : PresentationPage;
    }

    private static string NormalizePresentation(string? requestedPresentation)
    {
        if (string.Equals(requestedPresentation, PresentationPage, StringComparison.OrdinalIgnoreCase))
        {
            return PresentationPage;
        }

        if (string.Equals(requestedPresentation, PresentationModal, StringComparison.OrdinalIgnoreCase))
        {
            return PresentationModal;
        }

        return string.Empty;
    }

    private static string NormalizeEditorTab(string? editorTab)
    {
        if (string.Equals(editorTab, EditorTabExternal, StringComparison.OrdinalIgnoreCase))
        {
            return EditorTabExternal;
        }

        if (string.Equals(editorTab, EditorTabCollaboration, StringComparison.OrdinalIgnoreCase))
        {
            return EditorTabCollaboration;
        }

        if (string.Equals(editorTab, EditorTabSchedule, StringComparison.OrdinalIgnoreCase))
        {
            return EditorTabSchedule;
        }

        return EditorTabBasic;
    }

    private static string NormalizeProjectTab(string editorTab)
        => string.Equals(editorTab, EditorTabSchedule, StringComparison.OrdinalIgnoreCase)
            ? "harmonogram"
            : "zaznamy";

    private static string NormalizeDeleteTab(string? tab)
    {
        if (string.Equals(tab, "harmonogram", StringComparison.OrdinalIgnoreCase))
        {
            return "harmonogram";
        }

        return "zaznamy";
    }

    private string BuildDeleteRecordReturnUrl(int projektId, string? returnUrl, string? uiContext, string fallbackTab)
    {
        if (string.Equals(uiContext, "page", StringComparison.OrdinalIgnoreCase))
        {
            return BuildRestoreReturnUrl(projektId, returnUrl, fallbackTab);
        }

        return NormalizeLocalReturnUrl(returnUrl)
            ?? (Url.Action("Detail", "Projekty", new { id = projektId, tab = fallbackTab }) ?? $"/Projekty/Detail/{projektId}?tab={fallbackTab}");
    }

    private string BuildRestoreReturnUrl(int projektId, string? returnUrl, string fallbackTab)
    {
        var candidate = NormalizeLocalReturnUrl(returnUrl)
            ?? (Url.Action("Detail", "Projekty", new { id = projektId, tab = fallbackTab }) ?? $"/Projekty/Detail/{projektId}?tab={fallbackTab}");

        return QueryHelpers.AddQueryString(candidate, "restoreRecordEditorState", "1");
    }

    private string BuildMeetingDetailUrl(int meetingId)
        => Url.Action("Detail", "Jednani", new { id = meetingId }) ?? $"/Jednani/Detail/{meetingId}";

    private IActionResult RedirectRecordEditorSaveTarget(int projektId, string? returnUrl, string fallbackTab, string uiContext, int? meetingId)
    {
        if (string.Equals(uiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase) && meetingId.HasValue)
        {
            return Redirect(BuildMeetingDetailUrl(meetingId.Value));
        }

        return Redirect(BuildRestoreReturnUrl(projektId, returnUrl, fallbackTab));
    }

    private static string NormalizeRecordEditorUiContext(string? uiContext, int? meetingId)
    {
        if (string.Equals(uiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase)
            && meetingId.HasValue
            && meetingId.Value > 0)
        {
            return UiContextMeeting;
        }

        return UiContextProject;
    }

    private string? NormalizeLocalReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
    }
}
