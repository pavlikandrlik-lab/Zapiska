using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Data;
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
    private const string EditorTabBasic = "basic";
    private const string EditorTabExternal = "external";
    private const string EditorTabCollaboration = "collaboration";
    private const string EditorTabSchedule = "schedule";
    private const string UiContextProject = "project";
    private const string UiContextMeeting = "meeting";

    private readonly IRecordService _recordService;
    private readonly IProjectEditQuery _projectEditQuery;
    private readonly IRecordUiFlowResolver _recordUiFlowResolver;
    private readonly IHarvestScheduler _harvestScheduler;
    private readonly PmTrackerDbContext _db;
    private readonly IRecordEditLockService _editLockService;

    public ZaznamyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IRecordService recordService,
        IProjectEditQuery projectEditQuery,
        IRecordUiFlowResolver recordUiFlowResolver,
        IHarvestScheduler harvestScheduler,
        PmTrackerDbContext db,
        IRecordEditLockService editLockService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _recordService = recordService;
        _projectEditQuery = projectEditQuery;
        _recordUiFlowResolver = recordUiFlowResolver;
        _harvestScheduler = harvestScheduler;
        _db = db;
        _editLockService = editLockService;
    }

    /// <summary>
    /// Stránka záznamu (2026-07-14): read-only detail na trvalé URL (sdílitelný odkaz).
    /// Guard = přístup k projektu; žádné edit právo se nevyžaduje — stránka ukazuje totéž,
    /// co uživatel vidí na kartě v záložce Záznamy. Autorizace PŘED těžkými dotazy.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Detail(int id, string? returnUrl, CancellationToken ct = default)
    {
        var projektId = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();

        if (!CurrentUserContext.CanAccessProject(projektId.Value))
        {
            return NotFound();
        }

        // Odkaz „odkud skutečnost pochází" nabídneme jen tomu, kdo okno vyjádření smí otevřít —
        // jinak by ikona vedla na akci, kterou server odmítne. Samotné akce v okně (re-harvest,
        // vazby) mají vlastní oprávnění na serveru.
        var canOpenVyjadreni = CurrentUserContext.HasPermission(PermissionKeys.VyjadreniModalOpen, projektId.Value);
        var model = await _recordService.BuildRecordDetailPageAsync(projektId.Value, id, canOpenVyjadreni, ct);
        if (model is null) return NotFound();

        PrepareRecordDetailPagePresentation(model, returnUrl);
        SetProjectBreadcrumbs(
            model.ProjektId, model.ProjektNazev, model.ProjektZkratka,
            currentText: $"Záznam #{model.Summary.CisloViditelne}",
            backUrl: model.BackUrl);
        return View("~/Views/Projekty/ZaznamDetailPage.cshtml", model);
    }

    private void PrepareRecordDetailPagePresentation(ZaznamDetailPageViewModel model, string? returnUrl)
    {
        var projektId = model.ProjektId;
        var recordId = model.Summary.Id;

        model.CanEditRecord = RecordEditorAffordancePolicy.CanOpenEditor(CurrentUserContext, projektId);
        model.CanCreateScheduleProposal = model.Summary.JeUkol
            && CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, projektId);

        var pageUrl = Url.Action(nameof(Detail), new { id = recordId }) ?? $"/Zaznamy/Detail/{recordId}";
        model.EditUrl = Url.Action(nameof(Edit), new { id = recordId, projektId, returnUrl = pageUrl });
        model.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy",
            new { projektId, zaznamId = recordId, returnUrl = pageUrl });
        model.PrintPdfUrl = Url.Action("UkolTisk", "Export", new { zaznamId = recordId, projektId, autoPrint = true });
        model.PrintWordUrl = Url.Action("UkolWord", "Export", new { zaznamId = recordId, projektId });
        model.BackUrl = NormalizeLocalReturnUrl(returnUrl) ?? ProjektDetailTabUrl(projektId, "zaznamy");

        // Vyjádření: stejná presentation jako na kartě (oprávnění, filtr draft jednání).
        PrepareRecordCommentsPresentation(model.Comments, model.Summary);
        model.Comments.CurrentUserOsobaId = CurrentUserContext.OsobaId;
    }


    public async Task<IActionResult> Edit(int id, string? returnUrl, CancellationToken ct = default)
    {
        // Review finding S-2: autorizační check PŘED těžkou DB query a T5 harvest triggerem.
        // Dříve BuildZaznamEditAsync načetl celý model pro record, který uživatel nesmí editovat
        // (existence leak), a ScheduleHarvestForRecordAsync byl volán bez ohledu na autorizaci.
        var projektId = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();

        var canEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId.Value);
        // F4 redesign 2026-04-23: records.schedule.add write cesta smazána (F3.7). Inline editor
        // přístupný pouze uživatelům s records.edit nebo records.schedule.edit; návrhový workflow
        // (proposals.schedule.create) má vlastní endpoint a nepoužívá tento editor.
        var canManageSchedulePermission = CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projektId.Value);
        // Sdílená afordance-gate (RecordEditorAffordancePolicy) — stejnou funkci používá UI pro
        // zobrazení tužky/Upravit, takže se server a UI nemůžou rozejít (návrhový klíč editor neotvírá).
        if (!RecordEditorAffordancePolicy.CanOpenEditor(CurrentUserContext, projektId.Value))
        {
            return Forbid();
        }

        // Spec 2026-09-17 §4.2 — zámek karty. Získává se PO autorizaci, ale PŘED těžkým
        // dotazem i harvest triggerem: nemá smysl připravovat editor, který se nezobrazí.
        var lockResult = await _editLockService.TryAcquireAsync(id, CurrentUserContext.OsobaId, ct);
        if (!lockResult.Acquired)
        {
            return await BuildRecordEditLockedResultAsync(id, projektId.Value, lockResult, ct);
        }

        var model = await _projectEditQuery.GetEditModelAsync(id, ct);

        // canManageSchedule je platný jen pro záznamy kategorie "úkol" (JeUkolKategorie).
        var canManageSchedule = model.JeUkolKategorie && canManageSchedulePermission;

        // T5 trigger (Plán C, spec §8.2.1): otevření editoru spustí proaktivní
        // harvest vyjádření pro všechny externí vazby záznamu. Jen pro uživatele,
        // kteří mají records.edit (schedule-only role nemá business need otevírat
        // harvest flow).
        if (canEditRecord)
        {
            await _harvestScheduler.ScheduleHarvestForRecordAsync(id, ct).ConfigureAwait(false);
        }

        PrepareRecordEditorModel(model, returnUrl, canEditRecord, canManageSchedule);
        // C1 (2026-07-10): ← = model.BackUrl (origin returnUrl ?? tab=zaznamy / meeting detail).
        SetProjectBreadcrumbs(
            model.ProjektId, model.ProjektNazev, model.ProjektZkratka,
            currentText: model.IsCreate ? "Nový záznam" : $"Záznam #{model.CisloViditelne}",
            backUrl: model.BackUrl);
        return View("~/Views/Projekty/EditZaznamPage.cshtml", model);
    }

    /// <summary>
    /// Spec 2026-09-17 §4.3 — stránka „upravuje jiný uživatel". Jméno se formátuje
    /// sdíleným <see cref="PersonDisplayName"/>, aby se hlášky o souběhu neshodovaly jen náhodou.
    /// </summary>
    private async Task<IActionResult> BuildRecordEditLockedResultAsync(
        int zaznamId,
        int projektId,
        RecordEditLockResult lockResult,
        CancellationToken ct)
    {
        var holderName = lockResult.HolderOsobaId.HasValue
            ? await PersonDisplayNameQuery.ResolveAsync(_db, lockResult.HolderOsobaId.Value, ct)
            : PersonDisplayName.Unknown;

        var identity = await _db.Projekty.AsNoTracking()
            .Where(x => x.Id == projektId)
            .Select(x => new { x.CelyNazev, x.Zkratka })
            .FirstOrDefaultAsync(ct);
        var cisloViditelne = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == zaznamId)
            .Select(x => x.CisloViditelne ?? x.CisloZaznamu.ToString())
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var backUrl = Url.Action("Detail", "Zaznamy", new { id = zaznamId }) ?? $"/Zaznamy/Detail/{zaznamId}";
        var model = new RecordEditLockedViewModel
        {
            ZaznamId = zaznamId,
            ProjektId = projektId,
            ProjektNazev = identity?.CelyNazev ?? string.Empty,
            ProjektZkratka = identity?.Zkratka ?? string.Empty,
            CisloViditelne = cisloViditelne,
            HolderDisplayName = holderName,
            SinceLocal = (lockResult.HolderSinceUtc ?? DateTime.UtcNow).ToLocalTime(),
            RetryUrl = Url.Action("Edit", "Zaznamy", new { id = zaznamId }) ?? $"/Zaznamy/Edit/{zaznamId}",
            BackUrl = backUrl
        };

        SetProjectBreadcrumbs(
            model.ProjektId, model.ProjektNazev, model.ProjektZkratka,
            currentText: $"Záznam #{model.CisloViditelne}",
            backUrl: backUrl);
        return View("~/Views/Projekty/RecordEditLockedPage.cshtml", model);
    }

    [Authorize(Policy = "permission:records.edit")]
    public async Task<IActionResult> Create(int projektId, int? jednaniId, string? uiContext, string? returnUrl, CancellationToken ct = default)
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
            returnUrl,
            canEditRecord: true,
            canManageSchedule: model.JeUkolKategorie,
            uiContext: normalizedUiContext,
            meetingId: contextMeetingId);
        // C1 (2026-07-10): ← = model.BackUrl (origin returnUrl ?? tab=zaznamy / meeting detail).
        SetProjectBreadcrumbs(
            model.ProjektId, model.ProjektNazev, model.ProjektZkratka,
            currentText: model.IsCreate ? "Nový záznam" : $"Záznam #{model.CisloViditelne}",
            backUrl: model.BackUrl);
        return View("~/Views/Projekty/EditZaznamPage.cshtml", model);
    }

    private void PrepareRecordEditorModel(
        ZaznamEditViewModel model,
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
        // F4 redesign 2026-04-23: add-only režim v inline editoru deaktivován (F3.7 záznam
        // save cestu pro schedule-only smazal). Propagujeme false — JS větev pro "add" režim
        // zůstává jako defensive no-op (viz recordEditor/form.js schedulePermissionMode==="add").
        model.CanEditScheduleAddOnly = false;
        var existingPermissions = model.HarmonogramBlok.Permissions;
        var schedulePermissions = existingPermissions.IsScheduleLocked || existingPermissions.IsPlanLocked
            ? existingPermissions with { IsTaskCategory = model.JeUkolKategorie }
            : model.CanEditScheduleFull
                ? ScheduleEditorPermissionSet.ForFullEdit(model.JeUkolKategorie)
                : model.CanEditScheduleAddOnly
                    ? ScheduleEditorPermissionSet.ForAddOnly(model.JeUkolKategorie)
                    : existingPermissions with { IsTaskCategory = model.JeUkolKategorie };

        // Phase 5 (DESIGN-9-B + 6-C, 2026-05-01): obohatit Permissions o klíče řízené flagy.
        // CanEditManualActual = task category + ne-locked schedule + má records.schedule.edit klíč.
        var canEditScheduleDirect = CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, model.ProjektId);
        var canProposeSchedule = CurrentUserContext.HasPermission(PermissionKeys.ProposalsScheduleCreate, model.ProjektId);
        schedulePermissions = schedulePermissions with
        {
            CanEditScheduleDirect = canEditScheduleDirect,
            CanProposeSchedule = canProposeSchedule,
            CanEditManualActual = model.JeUkolKategorie
                && !schedulePermissions.IsScheduleLocked
                && canEditScheduleDirect
        };

        // `with`: zachová OverviewLayout/Today a ostatní pole, přepíše jen permissions-odvozené.
        model.HarmonogramBlok = model.HarmonogramBlok with
        {
            Permissions = schedulePermissions,
            CanEditManualActual = schedulePermissions.CanEditManualActual
        };
        model.ActiveEditorTab = !canEditRecord && canManageSchedule && model.JeUkolKategorie
            ? EditorTabSchedule
            : EditorTabBasic;
        model.UseAjaxSubmit = true;
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

    private string BuildMeetingDetailUrl(int meetingId)
        => Url.Action("Detail", "Jednani", new { id = meetingId }) ?? $"/Jednani/Detail/{meetingId}";
}
