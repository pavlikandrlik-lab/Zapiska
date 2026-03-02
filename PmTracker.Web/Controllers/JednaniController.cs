using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class JednaniController : BaseController
{
    public JednaniController(IPmTrackerDataStore dataStore, IUserContextResolver userContextResolver)
        : base(dataStore, userContextResolver)
    {
    }

    public IActionResult Index(int? projektId)
    {
        var projekty = DataStore.BuildJednaniOverview()
            .Where(project => CurrentUserContext.CanAccessProject(project.ProjektId))
            .ToList();

        if (projektId.HasValue)
        {
            projekty = projekty.Where(p => p.ProjektId == projektId.Value).ToList();
        }

        var model = new JednaniIndexViewModel
        {
            Projekty = projekty
        };

        return View(model);
    }

    public IActionResult Detail(int id, string? returnUrl)
    {
        var model = DataStore.BuildJednaniDetail(id);
        if (!CurrentUserContext.CanAccessProject(model.ProjektId))
        {
            return Forbid();
        }

        var fallbackUrl = Url.Action("Index", "Jednani", new { projektId = model.ProjektId }) ?? "/Jednani";
        var isValidReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl);

        ViewData["BackUrl"] = isValidReturnUrl ? returnUrl : fallbackUrl;
        ViewData["BackLabel"] = isValidReturnUrl ? "Zpět na projekt" : "Zpět na jednání";

        return View(model);
    }

    [HttpGet]
    public IActionResult TaskItemPartial(int jednaniId, int zaznamId)
    {
        var model = DataStore.BuildJednaniDetail(jednaniId);
        if (!CurrentUserContext.CanAccessProject(model.ProjektId))
        {
            return Forbid();
        }

        var ukol = model.Ukoly.FirstOrDefault(x => x.ZaznamId == zaznamId);
        if (ukol is null)
        {
            return NotFound();
        }

        var isLocked = !string.IsNullOrWhiteSpace(model.UzavrenyStavKod)
            && string.Equals(model.Jednani.StavKod, model.UzavrenyStavKod, StringComparison.OrdinalIgnoreCase);
        var canEditRecordNotes = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId) && !isLocked;
        var canCommentAsSubsystemLeader = CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, model.ProjektId)
            && ukol.SubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId)
            && !isLocked;
        if (!canEditRecordNotes && !canCommentAsSubsystemLeader)
        {
            return Forbid();
        }

        return PartialView("_TaskItemPartial", new JednaniTaskItemPartialViewModel
        {
            ProjektId = model.ProjektId,
            JednaniId = model.Jednani.Id,
            JednaniCislo = model.Jednani.CisloJednani,
            CanEditRecordNotes = canEditRecordNotes,
            CanCommentAsSubsystemLeader = canCommentAsSubsystemLeader,
            Ukol = ukol
        });
    }

    [HttpGet]
    public IActionResult AddMeetingParticipantModal(int projektId, int jednaniId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, projektId))
        {
            return Forbid();
        }

        var model = DataStore.BuildJednaniDetail(jednaniId);
        if (model.ProjektId != projektId)
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
            DostupneOsoby = model.AvailableParticipantCandidates
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveStatus(SaveMeetingStatusCommand command, string? returnUrl)
    {
        var meeting = DataStore.BuildJednaniDetail(command.JednaniId);
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, meeting.ProjektId))
        {
            return Forbid();
        }

        try
        {
            DataStore.SaveMeetingStatus(command, CurrentUserContext);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Detail), new { id = command.JednaniId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveAttendance(int projektId, int jednaniId, List<MeetingAttendanceRowInput> rows, string? returnUrl)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, projektId))
        {
            return Forbid();
        }

        try
        {
            foreach (var row in rows.Where(x => x.OsobaId > 0 && !string.IsNullOrWhiteSpace(x.StavUcasti)))
            {
                DataStore.SaveAttendance(new SaveAttendanceCommand
                {
                    JednaniId = jednaniId,
                    OsobaId = row.OsobaId,
                    StavUcasti = row.StavUcasti
                }, CurrentUserContext);
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Detail), new { id = jednaniId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddMeetingParticipant(AddMeetingParticipantCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId),
            invalidAjaxMessage: "Osobu nelze přidat do účasti.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.JednaniId }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.JednaniId }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "page",
                refreshUrl: Url.Action(nameof(Detail), new { id = command.JednaniId }),
                projectId: command.ProjektId,
                meetingId: command.JednaniId,
                uiContext: "meeting",
                message: "Osoba byla přidána do účasti."),
            operation: () => DataStore.AddMeetingParticipant(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveNotes(int projektId, int jednaniId, List<MeetingNoteRowInput> rows, string? returnUrl)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projektId))
        {
            return Forbid();
        }

        try
        {
            foreach (var row in rows.Where(x => x.ZaznamId > 0 && !string.IsNullOrWhiteSpace(x.Text)))
            {
                DataStore.SaveMeetingNote(new SaveMeetingNoteCommand
                {
                    JednaniId = jednaniId,
                    ZaznamId = row.ZaznamId,
                    Text = row.Text
                }, CurrentUserContext);
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Detail), new { id = jednaniId });
    }

    public sealed class MeetingNoteRowInput
    {
        public int ZaznamId { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class MeetingAttendanceRowInput
    {
        public int OsobaId { get; set; }
        public string StavUcasti { get; set; } = string.Empty;
    }
}
