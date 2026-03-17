using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Meetings;
using PmTracker.Web.Modules.Projects;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ProjektyController : BaseController
{
    private const string TeamTab = "tym";
    private const string TeamRefreshScope = "projekty-detail-tym";

    private readonly IProjectsQueries _projectsQueries;
    private readonly IProjectsCommands _projectsCommands;
    private readonly IMeetingsQueries _meetingsQueries;
    private readonly IMeetingsCommands _meetingsCommands;

    public ProjektyController(
        IUserContextResolver userContextResolver,
        IProjectsQueries projectsQueries,
        IProjectsCommands projectsCommands,
        IMeetingsQueries meetingsQueries,
        IMeetingsCommands meetingsCommands)
        : base(userContextResolver)
    {
        _projectsQueries = projectsQueries;
        _projectsCommands = projectsCommands;
        _meetingsQueries = meetingsQueries;
        _meetingsCommands = meetingsCommands;
    }

    public IActionResult Index()
    {
        var projekty = _projectsQueries.BuildProjektyList()
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
        if (!_projectsQueries.ProjektExists(id))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = _projectsQueries.BuildProjektDetail(id);
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

        var project = _projectsQueries.BuildProjektyList().FirstOrDefault(item => item.Id == id);
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

        var project = _projectsQueries.BuildProjektyList().FirstOrDefault(item => item.Id == id);
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

        if (!_meetingsQueries.ProjektExists(projektId))
        {
            return NotFound();
        }

        var detail = _meetingsQueries.BuildProjektDetail(projektId);
        var nextMeetingNumber = detail.Jednani.Any() ? detail.Jednani.Max(item => item.CisloJednani) + 1 : 1;
        var defaultStatus = detail.StavyJednani.FirstOrDefault()?.Value ?? string.Empty;
        var localNow = GetLocalNow();

        var model = new MeetingModalViewModel
        {
            Title = "Nová porada",
            Command = new SaveMeetingCommand
            {
                ProjektId = projektId,
                CisloJednani = nextMeetingNumber,
                DatumPlanovane = localNow.Date,
                CasZacatek = TimeOnly.FromDateTime(localNow),
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

        if (!_meetingsQueries.ProjektExists(projektId))
        {
            return NotFound();
        }

        var projectDetail = _meetingsQueries.BuildProjektDetail(projektId);
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

        if (!_projectsQueries.ProjektExists(projektId))
        {
            return NotFound();
        }

        var detail = _projectsQueries.BuildProjektDetail(projektId);
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

        var detail = _projectsQueries.BuildProjektDetail(projektId);
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

        var detail = _projectsQueries.BuildProjektDetail(projektId);
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

        var detail = _projectsQueries.BuildProjektDetail(projektId);
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
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = savedProjectId }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-index",
                refreshUrl: Url.Action(nameof(Index), "Projekty"),
                projectId: savedProjectId,
                message: "Projekt byl uložen."),
            operation: () => savedProjectId = _projectsCommands.SaveProject(command, CurrentUserContext),
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
            operation: () => _projectsCommands.SoftDeleteProject(command, CurrentUserContext),
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

            var projectDetail = _meetingsQueries.BuildProjektDetail(command.ProjektId);
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
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" }),
            onSuccessRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" }),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "projekty-detail-jednani",
                refreshUrl: Url.Action(nameof(Detail), "Projekty", new { id = command.ProjektId, tab = "jednani" }),
                projectId: command.ProjektId,
                tab: "jednani",
                message: "Porada byla uložena."),
            operation: () => _meetingsCommands.SaveMeeting(command, CurrentUserContext));
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
            operation: () => _meetingsCommands.DeleteMeeting(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveTeamMember(SaveTeamMemberCommand command)
    {
        return ExecuteTeamValidatedAction(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Člena týmu nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Člen týmu byl uložen.",
            operation: () => _projectsCommands.SaveTeamMember(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveTeamMember(RemoveTeamMemberCommand command)
    {
        return ExecuteTeamAction(
            projektId: command.ProjektId,
            operation: () => _projectsCommands.RemoveTeamMember(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignProjectRole(AssignProjectRoleCommand command)
    {
        return ExecuteTeamValidatedAction(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Projektovou roli nelze přiřadit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Projektová role byla přiřazena.",
            operation: () => _projectsCommands.AssignProjectRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeactivateProjectRole(DeactivateProjectRoleCommand command, int projektId)
    {
        return ExecuteTeamValidatedAction(
            projektId: projektId,
            invalidAjaxMessage: "Projektovou roli nelze deaktivovat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Projektová role byla deaktivována.",
            operation: () => _projectsCommands.DeactivateProjectRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignProjectSubsystem(AssignProjectSubsystemCommand command)
    {
        return ExecuteTeamValidatedAction(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Subsystém nelze přiřadit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Subsystém byl přiřazen k projektu.",
            operation: () => _projectsCommands.AssignProjectSubsystem(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, int projektId)
    {
        return ExecuteTeamValidatedAction(
            projektId: projektId,
            invalidAjaxMessage: "Subsystém projektu nelze deaktivovat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Subsystém projektu byl deaktivován.",
            operation: () => _projectsCommands.DeactivateProjectSubsystem(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command)
    {
        return ExecuteTeamValidatedAction(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Subsystemovou roli nelze přiřadit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Role v subsystému byla přiřazena.",
            operation: () => _projectsCommands.AssignProjectSubsystemRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, int projektId)
    {
        return ExecuteTeamValidatedAction(
            projektId: projektId,
            invalidAjaxMessage: "Subsystemovou roli nelze deaktivovat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Role v subsystému byla deaktivována.",
            operation: () => _projectsCommands.DeactivateProjectSubsystemRole(command, CurrentUserContext));
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
        return _projectsQueries.BuildProjectStatusOptions(CurrentUserContext);
    }

    private IActionResult ExecuteTeamValidatedAction(
        int projektId,
        string invalidAjaxMessage,
        string invalidFallbackMessage,
        string successMessage,
        Action operation)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId),
            invalidAjaxMessage: invalidAjaxMessage,
            invalidFallbackMessage: invalidFallbackMessage,
            onInvalidRedirect: () => RedirectToTeamTab(projektId),
            onSuccessRedirect: () => RedirectToTeamTab(projektId),
            onAjaxSuccess: () => BuildTeamAjaxSuccess(projektId, successMessage),
            operation: operation);
    }

    private IActionResult ExecuteTeamAction(int projektId, Action operation)
    {
        return ExecuteCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId),
            onSuccessRedirect: () => RedirectToTeamTab(projektId),
            onAjaxSuccess: null,
            operation: operation);
    }

    private JsonResult BuildTeamAjaxSuccess(int projektId, string successMessage)
    {
        return AjaxSuccessResult(
            refreshScope: TeamRefreshScope,
            refreshUrl: Url.Action(nameof(Detail), new { id = projektId, tab = TeamTab }),
            projectId: projektId,
            tab: TeamTab,
            message: successMessage);
    }

    private RedirectToActionResult RedirectToTeamTab(int projektId)
        => RedirectToAction(nameof(Detail), new { id = projektId, tab = TeamTab })!;
}
