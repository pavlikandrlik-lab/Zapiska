using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed partial class JednaniController : BaseController
{
    private readonly IMeetingService _meetingService;
    private readonly IProjectService _projectService;

    public JednaniController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IMeetingService meetingService,
        IProjectService projectService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _meetingService = meetingService;
        _projectService = projectService;
    }

    public async Task<IActionResult> Index(int? projektId, CancellationToken ct = default)
    {
        var projectFilter = BuildMeetingOverviewProjectFilter(projektId);
        var projekty = await _meetingService.BuildJednaniOverviewAsync(projectFilter, ct);

        return View(new JednaniIndexViewModel
        {
            CurrentUserContext = CurrentUserContext,
            PageTitle = "Jednání",
            Projekty = projekty
                .Select(project => new JednaniProjektListItemViewModel
                {
                    ProjektId = project.ProjektId,
                    ProjektNazev = project.ProjektNazev,
                    ProjektZkratka = project.ProjektZkratka,
                    ProjektStavKod = project.ProjektStavKod,
                    ProjektStav = project.ProjektStav,
                    MistoPlneni = project.MistoPlneni,
                    PocetLetos = project.PocetLetos,
                    PocetCelkem = project.PocetCelkem,
                    Jednani = project.Jednani,
                    RocniSkupiny = project.RocniSkupiny,
                    PreviewRok = MeetingYearGroupBuilder.ResolvePreviewYear(project.RocniSkupiny, GetLocalNow().Year),
                    CanDeleteMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, project.ProjektId),
                    CanCreateMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, project.ProjektId)
                })
                .ToList()
        });
    }

    public async Task<IActionResult> Detail(int id, string? returnUrl, CancellationToken ct = default)
    {
        var model = AttachCurrentUser(await _meetingService.BuildJednaniDetailAsync(id, ct));
        if (!CurrentUserContext.CanAccessProject(model.ProjektId))
        {
            return NotFound();
        }

        model.CanEditMeeting = CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, model.ProjektId);
        model.CanEditRecords = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId);
        // F4 redesign 2026-04-23: records.comment.subsystemlead → meetings.notes.subsystemlead.
        model.HasSubsystemLeadPermission = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, model.ProjektId);
        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.PageTitle = $"Jednání č. {model.Jednani.CisloJednani}";

        var fallbackUrl = Url.Action("Index", "Jednani", new { projektId = model.ProjektId }) ?? "/Jednani";
        var isValidReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl);

        model.BackUrl = isValidReturnUrl ? returnUrl : fallbackUrl;
        model.BackLabel = isValidReturnUrl ? "Zpět na projekt" : "Zpět na jednání";

        return View(model);
    }

    private IReadOnlyCollection<int>? BuildMeetingOverviewProjectFilter(int? projektId)
    {
        if (projektId.HasValue)
        {
            return CurrentUserContext.CanAccessProject(projektId.Value)
                ? [projektId.Value]
                : Array.Empty<int>();
        }

        if (CurrentUserContext.IsSuperAdmin || HasGlobalMeetingOverviewAccess())
        {
            return null;
        }

        var authz = CurrentUserContext.Authorization;

        // Per-project grants from the snapshot (INCLUDE scope).
        var snapshotProjectIds = authz is not null
            ? authz.PerProjectPermissions
                .Where(kvp => kvp.Value.Any(PermissionKeys.GrantsProjectRead))
                .Select(kvp => kvp.Key)
            : [];

        return CurrentUserContext.VisibleProjectIds
            .Concat(snapshotProjectIds)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
    }

    private bool HasGlobalMeetingOverviewAccess()
    {
        var authz = CurrentUserContext.Authorization;
        if (authz is null) return false;

        return authz.GlobalPermissions.Any(PermissionKeys.GrantsProjectRead);
    }

    [HttpGet]
    public async Task<IActionResult> TaskItemPartial(int jednaniId, int zaznamId, CancellationToken ct = default)
    {
        var projektId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (!projektId.HasValue)
        {
            return NotFound();
        }

        if (!CurrentUserContext.CanAccessProject(projektId.Value))
        {
            return NotFound();
        }

        var ukol = await _meetingService.GetSingleTaskAsync(jednaniId, zaznamId, ct);
        if (ukol is null)
        {
            return NotFound();
        }

        var meetings = await _meetingService.BuildJednaniListAsync(projektId.Value, ct);
        var meeting = meetings.FirstOrDefault(x => x.Id == jednaniId);
        if (meeting is null)
        {
            return NotFound();
        }

        var isRelevantSubsystemLeader = ukol.SubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        // F4 redesign 2026-04-23: "editace zápisu" = meetings.notes.edit (per-action);
        // subsystem lead = meetings.notes.subsystemlead + lead-equivalent + DRAFT.
        var canEditRecordNotes = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesEdit, projektId.Value)
            && ukol.LzeUpravovatVyjadreni;
        var isDraftMeeting = string.Equals(meeting.StavKod, "DRAFT", StringComparison.OrdinalIgnoreCase);
        var canCommentAsSubsystemLeader = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, projektId.Value)
            && isRelevantSubsystemLeader
            && isDraftMeeting
            && ukol.LzeUpravovatVyjadreni;
        if (!canEditRecordNotes && !canCommentAsSubsystemLeader && !isRelevantSubsystemLeader)
        {
            return Forbid();
        }

        return PartialView("_TaskItemPartial", new JednaniTaskItemPartialViewModel
        {
            ProjektId = projektId.Value,
            JednaniId = meeting.Id,
            JednaniCislo = meeting.CisloJednani,
            CanEditRecordNotes = canEditRecordNotes,
            CanCommentAsSubsystemLeader = canCommentAsSubsystemLeader,
            CurrentUserOsobaId = CurrentUserContext.OsobaId,
            Ukol = ukol
        });
    }

    [HttpGet]
    [Authorize(Policy = "permission:meetings.participant.add")]
    public async Task<IActionResult> AddMeetingParticipantModal(int projektId, int jednaniId, CancellationToken ct = default)
    {
        var actualProjectId = await _meetingService.GetMeetingProjectIdAsync(jednaniId, ct);
        if (actualProjectId != projektId)
        {
            return NotFound();
        }

        return View("~/Views/Jednani/AddMeetingParticipantModal.cshtml", new AddMeetingParticipantModalViewModel
        {
            Title = "Přidat osobu do účasti",
            Command = new AddMeetingParticipantCommand
            {
                ProjektId = projektId,
                JednaniId = jednaniId
            },
            DostupneOsoby = await _meetingService.BuildMeetingParticipantCandidatesAsync(projektId, jednaniId, ct)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStatus(SaveMeetingStatusCommand command, string? returnUrl, CancellationToken ct = default)
    {
        if (command is null) return BadRequest();
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return AjaxInvalidModelResult("Uložení stavu jednání selhalo.");
            TempData["ErrorMessage"] = InvalidFormFallbackMessage;
            return RedirectToAction(nameof(Detail), new { id = command.JednaniId })!;
        }

        var projektId = await _meetingService.GetMeetingProjectIdAsync(command.JednaniId, ct);
        if (!projektId.HasValue) return NotFound();
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsStatusChange, projektId.Value)) return Forbid();

        try
        {
            await _meetingService.SaveMeetingStatusAsync(command, CurrentUserContext, ct);
        }
        catch (InvalidOperationException ex)
        {
            if (IsAjaxRequest())
                return AjaxErrorResult(ex.Message);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id = command.JednaniId })!;
        }

        if (IsAjaxRequest())
        {
            return AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: Url.Action(nameof(Detail), new { id = command.JednaniId }),
                meetingId: command.JednaniId,
                uiContext: "meeting",
                message: "Stav jednání byl uložen.");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Detail), new { id = command.JednaniId })!;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAttendance(int projektId, int jednaniId, List<MeetingAttendanceRowInput> rows, string? returnUrl, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return AjaxInvalidModelResult("Uložení účasti selhalo.");
            TempData["ErrorMessage"] = InvalidFormFallbackMessage;
            return RedirectToAction(nameof(Detail), new { id = jednaniId })!;
        }

        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsAttendanceEdit, projektId))
            return Forbid();

        try
        {
            await _meetingService.SaveAttendanceBatchAsync(
                jednaniId,
                rows.Select(x => (x.OsobaId, x.StavUcasti ?? string.Empty)),
                CurrentUserContext,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            if (IsAjaxRequest())
                return AjaxErrorResult(ex.Message);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id = jednaniId })!;
        }

        if (IsAjaxRequest())
        {
            return AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: Url.Action(nameof(Detail), new { id = jednaniId }),
                projectId: projektId,
                meetingId: jednaniId,
                uiContext: "meeting",
                message: "Účast byla uložena.");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Detail), new { id = jednaniId })!;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddMeetingParticipant(AddMeetingParticipantCommand command, CancellationToken ct = default)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.MeetingsParticipantAdd, command.ProjektId),
            invalidAjaxMessage: "Osobu nelze přidat do účasti.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.JednaniId })!,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Detail), new { id = command.JednaniId })!),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: Url.Action(nameof(Detail), new { id = command.JednaniId }),
                projectId: command.ProjektId,
                meetingId: command.JednaniId,
                uiContext: "meeting",
                message: "Osoba byla přidána do účasti.")),
            operation: () => _meetingService.AddMeetingParticipantAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotes(int projektId, int jednaniId, List<MeetingNoteRowInput> rows, string? returnUrl, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
                return AjaxInvalidModelResult("Uložení poznámek selhalo.");
            TempData["ErrorMessage"] = InvalidFormFallbackMessage;
            return RedirectToAction(nameof(Detail), new { id = jednaniId })!;
        }

        // Duální gate: meetings.notes.edit (primární) nebo meetings.notes.subsystemlead
        // (subsystem lead smí přidat zápis za vedoucího — service filter restriktuje na
        // jeho subsystém).
        var canEditNotes = CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesEdit, projektId)
            || CurrentUserContext.HasPermission(PermissionKeys.MeetingsNotesSubsystemLead, projektId);
        if (!canEditNotes)
            return Forbid();

        try
        {
            await _meetingService.SaveMeetingNotesBatchAsync(
                jednaniId,
                rows.Select(x => (x.ZaznamId, x.Text ?? string.Empty)),
                CurrentUserContext,
                ct);
        }
        catch (InvalidOperationException ex)
        {
            if (IsAjaxRequest())
                return AjaxErrorResult(ex.Message);
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(Detail), new { id = jednaniId })!;
        }

        if (IsAjaxRequest())
        {
            return AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: Url.Action(nameof(Detail), new { id = jednaniId }),
                projectId: projektId,
                meetingId: jednaniId,
                uiContext: "meeting",
                message: "Poznámky byly uloženy.");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Detail), new { id = jednaniId })!;
    }

    // Text je nullable záměrně — viz poznámka u MeetingAttendanceRowInput.
    public sealed class MeetingNoteRowInput
    {
        public int ZaznamId { get; set; }
        public string? Text { get; set; }
    }

    // StavUcasti je nullable záměrně: prázdná hodnota v batch formuláři znamená
    // „přeskočit tuto osobu", service (SaveAttendanceBatchAsync) tyto řádky filtruje.
    // Non-nullable string by model binder automaticky flagoval jako Required a rozbíjel
    // non-ajax submit s částečně vyplněným gridem.
    public sealed class MeetingAttendanceRowInput
    {
        public int OsobaId { get; set; }
        public string? StavUcasti { get; set; }
    }
}
