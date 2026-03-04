using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ProjektyController : BaseController
{
    public ProjektyController(IPmTrackerDataStore dataStore, IUserContextResolver userContextResolver)
        : base(dataStore, userContextResolver)
    {
    }

    public IActionResult Index()
    {
        var projekty = DataStore.BuildProjektyList()
            .Where(project => CurrentUserContext.CanAccessProject(project.Id))
            .ToList();
        var projectStatusOptions = BuildProjectStatusOptions();

        var model = new ProjektyIndexViewModel
        {
            IsAdmin = CurrentUserContext.IsSuperAdmin,
            CanCreate = CurrentUserContext.HasPermission(PermissionKeys.ProjectsCreate),
            StavyProjektu = projectStatusOptions,
            Projekty = projekty
                .Select(p => new ProjektListItemViewModel
                {
                    Id = p.Id,
                    Zkratka = p.Zkratka,
                    Nazev = p.Nazev,
                    StavKod = p.StavKod,
                    Stav = p.Stav,
                    CanEdit = p.CanEdit && CurrentUserContext.HasPermission(PermissionKeys.ProjectsEdit, p.Id),
                    CanDelete = p.CanDelete && CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete, p.Id)
                })
                .ToList()
        };

        return View(model);
    }

    public IActionResult Detail(int id)
    {
        if (!DataStore.ProjektExists(id))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = DataStore.BuildProjektDetail(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult NewProjectModal()
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.ProjectsCreate))
        {
            return Forbid();
        }

        var statusOptions = BuildProjectStatusOptions();
        var defaultStatus = statusOptions
            .FirstOrDefault(option => string.Equals(option.Value, "PLAN", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?? statusOptions.FirstOrDefault()?.Value
            ?? string.Empty;

        var model = new ProjectModalViewModel
        {
            Title = "Nový projekt",
            Command = new SaveProjectCommand
            {
                Stav = defaultStatus,
                PouzivatIdentJednani = false
            },
            StavyProjektu = statusOptions
        };

        return View("ProjectModal", model);
    }

    [HttpGet]
    public IActionResult EditProjectModal(int id)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.ProjectsEdit, id))
        {
            return Forbid();
        }

        var project = DataStore.BuildProjektyList().FirstOrDefault(item => item.Id == id);
        if (project is null)
        {
            return NotFound();
        }

        var statusOptions = BuildProjectStatusOptions();
        var selectedStatus = statusOptions.Any(option => string.Equals(option.Value, project.StavKod, StringComparison.OrdinalIgnoreCase))
            ? project.StavKod
            : statusOptions.FirstOrDefault(option => string.Equals(option.Label, project.Stav, StringComparison.CurrentCultureIgnoreCase))?.Value;
        selectedStatus ??= statusOptions.FirstOrDefault()?.Value ?? string.Empty;

        var model = new ProjectModalViewModel
        {
            Title = "Upravit projekt",
            Command = new SaveProjectCommand
            {
                Id = project.Id,
                Nazev = project.Nazev,
                Zkratka = project.Zkratka,
                Stav = selectedStatus,
                PouzivatIdentJednani = project.PouzivatIdentJednani
            },
            StavyProjektu = statusOptions
        };

        return View("ProjectModal", model);
    }

    [HttpGet]
    public IActionResult DeleteProjectModal(int id)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete, id))
        {
            return Forbid();
        }

        var project = DataStore.BuildProjektyList().FirstOrDefault(item => item.Id == id);
        if (project is null)
        {
            return NotFound();
        }

        var model = new DeleteProjectModalViewModel
        {
            Title = "Smazat projekt",
            Command = new SoftDeleteProjectCommand
            {
                ProjektId = id
            },
            ProjektNazev = project.Nazev,
            ProjektZkratka = project.Zkratka,
            ProjektStav = project.Stav
        };

        return View("DeleteProjectModal", model);
    }

    [HttpGet]
    public IActionResult NewMeetingModal(int projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, projektId))
        {
            return Forbid();
        }

        if (!DataStore.ProjektExists(projektId))
        {
            return NotFound();
        }

        var detail = DataStore.BuildProjektDetail(projektId);
        var nextMeetingNumber = detail.Jednani.Any() ? detail.Jednani.Max(item => item.CisloJednani) + 1 : 1;
        var defaultStatus = detail.StavyJednani.FirstOrDefault()?.Value ?? string.Empty;

        var model = new MeetingModalViewModel
        {
            Title = "Nová porada",
            Command = new SaveMeetingCommand
            {
                ProjektId = projektId,
                CisloJednani = nextMeetingNumber,
                DatumPlanovane = DateTime.Today,
                CasZacatek = TimeOnly.FromDateTime(DateTime.Now),
                StavJednani = defaultStatus
            },
            ExistingMeetingNumbersCsv = string.Join(",", detail.Jednani.Select(item => item.CisloJednani).Distinct().OrderBy(item => item)),
            StavyJednani = detail.StavyJednani
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult EditMeetingModal(int projektId, int meetingId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, projektId))
        {
            return Forbid();
        }

        if (!DataStore.ProjektExists(projektId))
        {
            return NotFound();
        }

        var projectDetail = DataStore.BuildProjektDetail(projektId);
        var meeting = projectDetail.Jednani.FirstOrDefault(item => item.Id == meetingId);
        if (meeting is null)
        {
            return NotFound();
        }

        if (IsMeetingClosed(projectDetail.StavyJednani, meeting))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var model = new MeetingModalViewModel
        {
            Title = "Upravit poradu",
            Command = new SaveMeetingCommand
            {
                Id = meeting.Id,
                ProjektId = projektId,
                CisloJednani = meeting.CisloJednani,
                DatumPlanovane = meeting.Datum,
                CasZacatek = meeting.CasZacatek,
                Misto = meeting.Misto,
                StavJednani = meeting.StavKod ?? projectDetail.StavyJednani.FirstOrDefault()?.Value ?? string.Empty
            },
            ExistingMeetingNumbersCsv = string.Join(",", projectDetail.Jednani.Select(item => item.CisloJednani).Distinct().OrderBy(item => item)),
            StavyJednani = projectDetail.StavyJednani
        };

        return View("NewMeetingModal", model);
    }

    [HttpGet]
    public IActionResult AddTeamMemberModal(int projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        if (!DataStore.ProjektExists(projektId))
        {
            return NotFound();
        }

        var detail = DataStore.BuildProjektDetail(projektId);
        var defaultRole = detail.RoleProjektu.FirstOrDefault()?.Value ?? string.Empty;
        var model = new TeamMemberModalViewModel
        {
            Title = "Přidat člena týmu",
            Command = new SaveTeamMemberCommand
            {
                ProjektId = projektId,
                Role = defaultRole
            },
            DostupniClenoveTymu = detail.DostupneOsobyProRole
                .Select(person => new TeamCandidateViewModel
                {
                    Id = person.OsobaId,
                    Osoba = person.Osoba,
                    Email = person.Email,
                    Organizace = person.Organizace,
                    OrganizacniCelek = person.OrganizacniCelek
                })
                .ToList(),
            RoleProjektu = detail.RoleProjektu
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult AssignProjectRoleModal(int projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var detail = DataStore.BuildProjektDetail(projektId);
        return View("AssignProjectRoleModal", new AssignProjectRoleModalViewModel
        {
            Title = "Přidat projektovou roli",
            Command = new AssignProjectRoleCommand { ProjektId = projektId },
            DostupneOsoby = detail.DostupneOsobyProRole,
            RoleProjektu = detail.RoleProjektu
        });
    }

    [HttpGet]
    public IActionResult AssignProjectSubsystemModal(int projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var detail = DataStore.BuildProjektDetail(projektId);
        return View("AssignProjectSubsystemModal", new AssignProjectSubsystemModalViewModel
        {
            Title = "Přiřadit subsystém projektu",
            Command = new AssignProjectSubsystemCommand { ProjektId = projektId },
            Subsystemy = detail.DostupneSubsystemy
        });
    }

    [HttpGet]
    public IActionResult AssignProjectSubsystemRoleModal(int projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var detail = DataStore.BuildProjektDetail(projektId);
        return View("AssignProjectSubsystemRoleModal", new AssignProjectSubsystemRoleModalViewModel
        {
            Title = "Přidat roli v subsystému",
            Command = new AssignProjectSubsystemRoleCommand { ProjektId = projektId },
            ProjektSubsystemy = detail.DostupneProjektoveSubsystemy,
            DostupneOsoby = detail.DostupneOsobyProRole,
            RoleSubsystemu = detail.RoleSubsystemu
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveProject(SaveProjectCommand command)
    {
        var isCreate = !command.Id.HasValue;
        var savedProjectId = 0;

        return ExecuteValidatedCommand(
            hasPermission: () => isCreate
                ? CurrentUserContext.HasPermission(PermissionKeys.ProjectsCreate)
                : CurrentUserContext.HasPermission(PermissionKeys.ProjectsEdit, command.Id),
            invalidAjaxMessage: "Projekt nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = savedProjectId }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-index",
                refreshUrl: Url.Action(nameof(Index), "Projekty"),
                projectId: savedProjectId,
                message: "Projekt byl uložen."),
            operation: () => savedProjectId = DataStore.SaveProject(command, CurrentUserContext),
            onExceptionRedirect: _ => RedirectToAction(nameof(Index)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteProject(SoftDeleteProjectCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete, command.ProjektId),
            invalidAjaxMessage: "Projekt nelze smazat.",
            invalidFallbackMessage: "Potvrďte smazání projektu.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => RedirectToAction(nameof(Index)),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-index",
                refreshUrl: Url.Action(nameof(Index), "Projekty"),
                projectId: command.ProjektId,
                message: "Projekt byl smazán."),
            operation: () => DataStore.SoftDeleteProject(command, CurrentUserContext),
            onExceptionRedirect: _ => RedirectToAction(nameof(Index)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveMeeting(SaveMeetingCommand command)
    {
        EnsureReadableMeetingTimeError();

        if (command.Id.HasValue)
        {
            if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId))
            {
                return Forbid();
            }

            var projectDetail = DataStore.BuildProjektDetail(command.ProjektId);
            var existingMeeting = projectDetail.Jednani.FirstOrDefault(item => item.Id == command.Id.Value);
            if (existingMeeting is null)
            {
                return NotFound();
            }

            if (IsMeetingClosed(projectDetail.StavyJednani, existingMeeting))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        return ExecuteValidatedCommand(
            hasPermission: () => command.Id.HasValue
                ? CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId)
                : CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, command.ProjektId),
            invalidAjaxMessage: "Poradu nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-jednani",
                refreshUrl: Url.Action(nameof(Detail), "Projekty", new { id = command.ProjektId, tab = "jednani" }),
                projectId: command.ProjektId,
                tab: "jednani",
                message: "Porada byla uložena."),
            operation: () => DataStore.SaveMeeting(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteMeeting(DeleteMeetingCommand command, string? returnUrl)
    {
        IActionResult RedirectAfterDelete()
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" });
        }

        return ExecuteCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId),
            onSuccessRedirect: RedirectAfterDelete,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-jednani",
                refreshUrl: Url.Action(nameof(Detail), "Projekty", new { id = command.ProjektId, tab = "jednani" }),
                projectId: command.ProjektId,
                tab: "jednani",
                message: "Porada byla smazána."),
            operation: () => DataStore.DeleteMeeting(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveTeamMember(SaveTeamMemberCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, command.ProjektId),
            invalidAjaxMessage: "Člena týmu nelze uložit.",
            invalidFallbackMessage: "formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-tym",
                refreshUrl: Url.Action(nameof(Detail), "Projekty", new { id = command.ProjektId, tab = "tym" }),
                projectId: command.ProjektId,
                tab: "tym",
                message: "Člen týmu byl uložen."),
            operation: () => DataStore.SaveTeamMember(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveTeamMember(RemoveTeamMemberCommand command)
    {
        return ExecuteCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, command.ProjektId),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onAjaxSuccess: null,
            operation: () => DataStore.RemoveTeamMember(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignProjectRole(AssignProjectRoleCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, command.ProjektId),
            invalidAjaxMessage: "Projektovou roli nelze přiřadit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-tym",
                refreshUrl: Url.Action(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
                projectId: command.ProjektId,
                tab: "tym",
                message: "Projektová role byla přiřazena."),
            operation: () => DataStore.AssignProjectRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeactivateProjectRole(DeactivateProjectRoleCommand command, int projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId),
            invalidAjaxMessage: "Projektovou roli nelze deaktivovat.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = projektId, tab = "tym" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = projektId, tab = "tym" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-tym",
                refreshUrl: Url.Action(nameof(Detail), new { id = projektId, tab = "tym" }),
                projectId: projektId,
                tab: "tym",
                message: "Projektová role byla deaktivována."),
            operation: () => DataStore.DeactivateProjectRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignProjectSubsystem(AssignProjectSubsystemCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, command.ProjektId),
            invalidAjaxMessage: "Subsystém nelze přiřadit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-tym",
                refreshUrl: Url.Action(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
                projectId: command.ProjektId,
                tab: "tym",
                message: "Subsystém byl přiřazen k projektu."),
            operation: () => DataStore.AssignProjectSubsystem(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, int projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId),
            invalidAjaxMessage: "Subsystém projektu nelze deaktivovat.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = projektId, tab = "tym" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = projektId, tab = "tym" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-tym",
                refreshUrl: Url.Action(nameof(Detail), new { id = projektId, tab = "tym" }),
                projectId: projektId,
                tab: "tym",
                message: "Subsystém projektu byl deaktivován."),
            operation: () => DataStore.DeactivateProjectSubsystem(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, command.ProjektId),
            invalidAjaxMessage: "Subsystemovou roli nelze přiřadit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-tym",
                refreshUrl: Url.Action(nameof(Detail), new { id = command.ProjektId, tab = "tym" }),
                projectId: command.ProjektId,
                tab: "tym",
                message: "Role v subsystému byla přiřazena."),
            operation: () => DataStore.AssignProjectSubsystemRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, int projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId),
            invalidAjaxMessage: "Subsystemovou roli nelze deaktivovat.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = projektId, tab = "tym" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = projektId, tab = "tym" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-tym",
                refreshUrl: Url.Action(nameof(Detail), new { id = projektId, tab = "tym" }),
                projectId: projektId,
                tab: "tym",
                message: "Role v subsystému byla deaktivována."),
            operation: () => DataStore.DeactivateProjectSubsystemRole(command, CurrentUserContext));
    }

    private void EnsureReadableMeetingTimeError()
    {
        var keys = new[] { nameof(SaveMeetingCommand.CasZacatek), $"command.{nameof(SaveMeetingCommand.CasZacatek)}" };
        var hasCasError = keys.Any(key => ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0);
        if (!hasCasError)
        {
            return;
        }

        var hasReadableError = keys
            .Where(key => ModelState.TryGetValue(key, out _))
            .SelectMany(key => ModelState[key]!.Errors)
            .Any(error => !string.IsNullOrWhiteSpace(error.ErrorMessage)
                          && error.ErrorMessage.Contains("HH:mm", StringComparison.OrdinalIgnoreCase));

        if (!hasReadableError)
        {
            ModelState.AddModelError(nameof(SaveMeetingCommand.CasZacatek), "Vyberte čas začátku ve formátu HH:mm.");
        }
    }

    private static bool IsMeetingClosed(IReadOnlyList<LookupOptionViewModel> statuses, JednaniListItemViewModel meeting)
    {
        var closedStatusCode = statuses
            .Where(item => string.Equals(item.Value, "CLOSED", StringComparison.OrdinalIgnoreCase)
                || item.Label.Contains("uzav", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Value)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(closedStatusCode))
        {
            return string.Equals(meeting.StavKod, closedStatusCode, StringComparison.OrdinalIgnoreCase);
        }

        return !string.IsNullOrWhiteSpace(meeting.UzamklOsoba);
    }

    private IReadOnlyList<LookupOptionViewModel> BuildProjectStatusOptions()
    {
        return DataStore.BuildCiselnikDetail("stavy-projektu", CurrentUserContext).Polozky
            .OrderBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Kod,
                Label = x.Nazev
            })
            .ToList();
    }
}
