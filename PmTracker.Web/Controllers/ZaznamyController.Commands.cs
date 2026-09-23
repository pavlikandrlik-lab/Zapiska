using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed partial class ZaznamyController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SaveRecordCommand command, CancellationToken ct = default)
    {
        var editorTab = NormalizeEditorTab(command.EditorTab);
        var projectTab = NormalizeProjectTab(editorTab);
        // Per-action redesign 2026-04-23: Save větev podle isUpdate:
        //   - nový záznam (Id == null):     records.create
        //   - editace záznamu:               records.edit
        //   - jen editace harmonogramu:      records.schedule.edit (oddělená větev, admin jen schedule slice)
        var isCreate = !command.Id.HasValue;
        var canCreateRecord = isCreate && CurrentUserContext.HasPermission(PermissionKeys.RecordsCreate, command.ProjektId);
        var canEditRecord = !isCreate && CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, command.ProjektId);
        var canEditSchedule = CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, command.ProjektId);
        var canSaveScheduleOnly = !isCreate
            && string.Equals(editorTab, EditorTabSchedule, StringComparison.OrdinalIgnoreCase)
            && canEditSchedule;
        var normalizedUiContext = NormalizeRecordEditorUiContext(command.UiContext, command.MeetingId);
        var contextMeetingId = string.Equals(normalizedUiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase)
            ? command.MeetingId
            : null;
        // savedRecordId je nastaven až v operation; redirect/AJAX lambdy se ale vyhodnocují AŽ po ní,
        // takže do cílové URL doplní recordId (deep-link → sjet na záznam a vysvítit ho).
        var savedRecordId = 0;
        var redirectAfterSave = () => RedirectRecordEditorSaveTarget(command.ProjektId, command.ReturnUrl, projectTab, normalizedUiContext, contextMeetingId, savedRecordId);

        return await ExecuteValidatedCommandAsync(
            hasPermission: () => canCreateRecord || canEditRecord || canSaveScheduleOnly,
            invalidAjaxMessage: "Záznam nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: redirectAfterSave,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(redirectAfterSave()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildSaveAjaxSuccessResult(
                command,
                savedRecordId,
                projectTab,
                normalizedUiContext,
                contextMeetingId)),
            operation: async () =>
            {
                savedRecordId = await _recordService.SaveRecordAsync(command, CurrentUserContext, ct);

                // Spec 2026-09-17 §4.2 — po uložení editor končí (klient dělá plnou navigaci),
                // takže zámek nemá co držet. Best-effort: selhání uvolnění nesmí shodit
                // už uložený záznam, nejhorší následek je zámek držený do vypršení TTL.
                if (command.Id.HasValue)
                {
                    try
                    {
                        await _editLockService.ReleaseAsync(command.Id.Value, CurrentUserContext.OsobaId, ct);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex,
                            "Uvolnění zámku záznamu {ZaznamId} selhalo; vyprší TTL.", command.Id.Value);
                    }
                }
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRecord(DeleteRecordCommand command, string? returnUrl, string? uiContext, string? tab, CancellationToken ct = default)
    {
        var normalizedTab = NormalizeDeleteTab(tab);
        var redirectUrl = BuildDeleteRecordReturnUrl(command.ProjektId, returnUrl, uiContext, normalizedTab);
        var redirect = () => Redirect(redirectUrl);

        return await ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.RecordsDelete, command.ProjektId),
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
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.RecordsAssignMeeting, command.ProjektId),
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:comments.add")]
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
    [Authorize(Policy = "permission:comments.edit.own")]
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
    [Authorize(Policy = "permission:comments.delete.own")]
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
        string normalizedUiContext,
        int? contextMeetingId)
    {
        var meetingDetailUrl = contextMeetingId.HasValue
            ? BuildMeetingDetailUrl(contextMeetingId.Value)
            : null;
        var refreshUrl = string.Equals(normalizedUiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(meetingDetailUrl)
            ? meetingDetailUrl
            : BuildRestoreReturnUrl(command.ProjektId, command.ReturnUrl, projectTab, savedRecordId);
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
            refreshUrl: Url.Action("RecordsTabPartial", "Projekty", new { id = projektId }) ?? $"/Projekty/RecordsTabPartial/{projektId}",
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

    private string BuildDeleteRecordReturnUrl(int projektId, string? returnUrl, string? uiContext, string fallbackTab)
    {
        if (string.Equals(uiContext, "page", StringComparison.OrdinalIgnoreCase))
        {
            return BuildRestoreReturnUrl(projektId, returnUrl, fallbackTab);
        }

        return NormalizeLocalReturnUrl(returnUrl)
            ?? (Url.Action("Detail", "Projekty", new { id = projektId, tab = fallbackTab }) ?? $"/Projekty/Detail/{projektId}?tab={fallbackTab}");
    }

    private string BuildRestoreReturnUrl(int projektId, string? returnUrl, string fallbackTab, int focusRecordId = 0)
    {
        var candidate = NormalizeLocalReturnUrl(returnUrl)
            ?? (Url.Action("Detail", "Projekty", new { id = projektId, tab = fallbackTab }) ?? $"/Projekty/Detail/{projektId}?tab={fallbackTab}");

        candidate = QueryHelpers.AddQueryString(candidate, "restoreRecordEditorState", "1");
        if (focusRecordId > 0)
        {
            // Deep-link na uložený záznam — na záložce Záznamy se sjede + vysvítí (initProjectRecordDeepLink).
            candidate = SetSingleQueryValue(candidate, "recordId", focusRecordId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return candidate;
    }

    // Nastaví (nebo přepíše) jednu query hodnotu — brání duplicitnímu recordId, kdyby ho returnUrl už nesl.
    private static string SetSingleQueryValue(string url, string key, string value)
    {
        var qIndex = url.IndexOf('?');
        var path = qIndex < 0 ? url : url[..qIndex];
        var query = qIndex < 0 ? string.Empty : url[qIndex..];
        var parsed = QueryHelpers.ParseQuery(query);
        var pairs = parsed
            .Where(kv => !string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());
        pairs[key] = value;
        return QueryHelpers.AddQueryString(path, pairs);
    }

    private IActionResult RedirectRecordEditorSaveTarget(int projektId, string? returnUrl, string fallbackTab, string uiContext, int? meetingId, int savedRecordId)
    {
        if (string.Equals(uiContext, UiContextMeeting, StringComparison.OrdinalIgnoreCase) && meetingId.HasValue)
        {
            return Redirect(BuildMeetingDetailUrl(meetingId.Value));
        }

        return Redirect(BuildRestoreReturnUrl(projektId, returnUrl, fallbackTab, savedRecordId));
    }
}
