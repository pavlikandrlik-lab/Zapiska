using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Records;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed partial class ZaznamyController : BaseController
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
    private readonly IHarvestScheduler _harvestScheduler;

    public ZaznamyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IRecordService recordService,
        IRecordUiFlowResolver recordUiFlowResolver,
        IHarvestScheduler harvestScheduler)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _recordService = recordService;
        _recordUiFlowResolver = recordUiFlowResolver;
        _harvestScheduler = harvestScheduler;
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

        // T5 trigger (Plán C, spec §8.2.1): otevření editoru spustí proaktivní
        // harvest vyjádření pro všechny externí vazby záznamu. Fire-and-forget —
        // scheduler implementace zajišťuje async execution a error isolation.
        await _harvestScheduler.ScheduleHarvestForRecordAsync(id, ct).ConfigureAwait(false);

        PrepareRecordEditorModel(model, presentation, returnUrl, canEditRecord, canManageSchedule);
        return View(GetEditorViewPath(model.Presentation), model);
    }

    [Authorize(Policy = "permission:records.edit")]
    public async Task<IActionResult> Create(int projektId, int? jednaniId, string? uiContext, string? presentation, string? returnUrl, CancellationToken ct = default)
    {
        if (!await _recordService.ProjektExistsAsync(projektId, ct))
        {
            return RedirectToAction("Index", "Projekty");
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
        var existingPermissions = model.HarmonogramBlok.Permissions;
        var schedulePermissions = existingPermissions.IsScheduleLocked || existingPermissions.IsPlanLocked
            ? existingPermissions with { IsTaskCategory = model.JeUkolKategorie }
            : model.CanEditScheduleFull
                ? ScheduleEditorPermissionSet.ForFullEdit(model.JeUkolKategorie)
                : model.CanEditScheduleAddOnly
                    ? ScheduleEditorPermissionSet.ForAddOnly(model.JeUkolKategorie)
                    : existingPermissions with { IsTaskCategory = model.JeUkolKategorie };
        model.HarmonogramBlok = new HarmonogramBlockViewModel
        {
            RecordId = model.HarmonogramBlok.RecordId,
            Mode = model.HarmonogramBlok.Mode,
            DatumZalozeni = model.HarmonogramBlok.DatumZalozeni,
            TerminUkonceni = model.HarmonogramBlok.TerminUkonceni,
            DelayBarvaHex = model.HarmonogramBlok.DelayBarvaHex,
            Souhrn = model.HarmonogramBlok.Souhrn,
            Kroky = model.HarmonogramBlok.Kroky,
            Permissions = schedulePermissions,
            EditorChangedTypeTooltips = model.HarmonogramBlok.EditorChangedTypeTooltips,
            ScheduleVersion = model.HarmonogramBlok.ScheduleVersion
        };
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

    private string BuildMeetingDetailUrl(int meetingId)
        => Url.Action("Detail", "Jednani", new { id = meetingId }) ?? $"/Jednani/Detail/{meetingId}";
}
