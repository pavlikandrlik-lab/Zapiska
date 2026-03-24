using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed class ProjektyController : BaseController
{
    private const string TeamTab = "tym";
    private const string TeamRefreshScope = "projekty-detail-tym";

    private readonly IProjectService _projectService;
    private readonly IMeetingService _meetingService;

    public ProjektyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IProjectService projectService,
        IMeetingService meetingService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _projectService = projectService;
        _meetingService = meetingService;
    }

    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var projekty = (await _projectService.BuildProjektyListAsync(ct))
            .Where(project => CurrentUserContext.CanAccessProject(project.Id))
            .ToList();
        var projectStatusOptions = await BuildProjectStatusOptionsAsync(ct);

        var model = new ProjektyIndexViewModel
        {
            PageTitle = "Projekty",
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

    public async Task<IActionResult> Detail(int id, CancellationToken ct = default)
    {
        if (!await _projectService.ProjektExistsAsync(id, ct))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjektDetailAsync(id, ct);
        PrepareProjectDetailPresentation(model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> NewProjectModal(CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.ProjectsCreate))
        {
            return Forbid();
        }

        var statusOptions = await BuildProjectStatusOptionsAsync(ct);
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
    public async Task<IActionResult> EditProjectModal(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.ProjectsEdit, id))
        {
            return Forbid();
        }

        var project = (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id);
        if (project is null)
        {
            return NotFound();
        }

        var statusOptions = await BuildProjectStatusOptionsAsync(ct);
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
    public async Task<IActionResult> DeleteProjectModal(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete, id))
        {
            return Forbid();
        }

        var project = (await _projectService.BuildProjektyListAsync(ct)).FirstOrDefault(item => item.Id == id);
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
    public async Task<IActionResult> NewMeetingModal(int projektId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, projektId))
        {
            return Forbid();
        }

        if (!await _projectService.ProjektExistsAsync(projektId, ct))
        {
            return NotFound();
        }

        var detail = await _projectService.BuildProjektDetailAsync(projektId, ct);
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
    public async Task<IActionResult> EditMeetingModal(int projektId, int meetingId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, projektId))
        {
            return Forbid();
        }

        if (!await _projectService.ProjektExistsAsync(projektId, ct))
        {
            return NotFound();
        }

        var projectDetail = await _projectService.BuildProjektDetailAsync(projektId, ct);
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
    public async Task<IActionResult> AddTeamMemberModal(int projektId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        if (!await _projectService.ProjektExistsAsync(projektId, ct))
        {
            return NotFound();
        }

        var detail = await _projectService.BuildProjektDetailAsync(projektId, ct);
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
    public async Task<IActionResult> AssignProjectRoleModal(int projektId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var detail = await _projectService.BuildProjektDetailAsync(projektId, ct);
        return View("AssignProjectRoleModal", new AssignProjectRoleModalViewModel
        {
            Title = "Přidat projektovou roli",
            Command = new AssignProjectRoleCommand { ProjektId = projektId },
            DostupneOsoby = detail.DostupneOsobyProRole,
            RoleProjektu = detail.RoleProjektu
        });
    }

    [HttpGet]
    public async Task<IActionResult> AssignProjectSubsystemModal(int projektId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var detail = await _projectService.BuildProjektDetailAsync(projektId, ct);
        return View("AssignProjectSubsystemModal", new AssignProjectSubsystemModalViewModel
        {
            Title = "Přiřadit subsystém projektu",
            Command = new AssignProjectSubsystemCommand { ProjektId = projektId },
            Subsystemy = detail.DostupneSubsystemy
        });
    }

    [HttpGet]
    public async Task<IActionResult> AssignProjectSubsystemRoleModal(int projektId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var detail = await _projectService.BuildProjektDetailAsync(projektId, ct);
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
    public Task<IActionResult> SaveProject(SaveProjectCommand command, CancellationToken ct = default)
    {
        var isCreate = !command.Id.HasValue;
        var savedProjectId = 0;

        return ExecuteValidatedCommandAsync(
            hasPermission: () => isCreate
                ? CurrentUserContext.HasPermission(PermissionKeys.ProjectsCreate)
                : CurrentUserContext.HasPermission(PermissionKeys.ProjectsEdit, command.Id),
            invalidAjaxMessage: "Projekt nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Detail), new { id = savedProjectId })!),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "projekty-index",
                refreshUrl: Url.Action(nameof(Index), "Projekty"),
                projectId: savedProjectId,
                message: "Projekt byl uložen.")),
            operation: async () => savedProjectId = await _projectService.SaveProjectAsync(command, CurrentUserContext, ct),
            onExceptionRedirect: _ => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index))!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteProject(SoftDeleteProjectCommand command, CancellationToken ct = default)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.ProjectsDelete, command.ProjektId),
            invalidAjaxMessage: "Projekt nelze smazat.",
            invalidFallbackMessage: "Potvrďte smazání projektu.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index)),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index))!),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "projekty-index",
                refreshUrl: Url.Action(nameof(Index), "Projekty"),
                projectId: command.ProjektId,
                message: "Projekt byl smazán.")),
            operation: () => _projectService.SoftDeleteProjectAsync(command, CurrentUserContext, ct),
            onExceptionRedirect: _ => Task.FromResult<IActionResult>(RedirectToAction(nameof(Index))!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMeeting(SaveMeetingCommand command, CancellationToken ct = default)
    {
        EnsureReadableMeetingTimeError();

        if (command.Id.HasValue)
        {
            if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId))
            {
                return Forbid();
            }

            var projectDetail = await _projectService.BuildProjektDetailAsync(command.ProjektId, ct);
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

        return await ExecuteValidatedCommandAsync(
            hasPermission: () => command.Id.HasValue
                ? CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId)
                : CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, command.ProjektId),
            invalidAjaxMessage: "Poradu nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" }),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" })!),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "projekty-detail-jednani",
                refreshUrl: Url.Action(nameof(Detail), "Projekty", new { id = command.ProjektId, tab = "jednani" }),
                projectId: command.ProjektId,
                tab: "jednani",
                message: "Porada byla uložena.")),
            operation: () => _meetingService.SaveMeetingAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteMeeting(DeleteMeetingCommand command, string? returnUrl, CancellationToken ct = default)
    {
        IActionResult RedirectAfterDelete()
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Detail), new { id = command.ProjektId, tab = "jednani" });
        }

        return ExecuteCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectAfterDelete()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "projekty-detail-jednani",
                refreshUrl: Url.Action(nameof(Detail), "Projekty", new { id = command.ProjektId, tab = "jednani" }),
                projectId: command.ProjektId,
                tab: "jednani",
                message: "Porada byla smazána.")),
            operation: () => _meetingService.DeleteMeetingAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveTeamMember(SaveTeamMemberCommand command, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Člena týmu nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Člen týmu byl uložen.",
            operation: () => _projectService.SaveTeamMemberAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveTeamMember(RemoveTeamMemberCommand command, CancellationToken ct = default)
    {
        return ExecuteTeamActionAsync(
            projektId: command.ProjektId,
            operation: () => _projectService.RemoveTeamMemberAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AssignProjectRole(AssignProjectRoleCommand command, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Projektovou roli nelze přiřadit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Projektová role byla přiřazena.",
            operation: () => _projectService.AssignProjectRoleAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateProjectRole(DeactivateProjectRoleCommand command, int projektId, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: projektId,
            invalidAjaxMessage: "Projektovou roli nelze deaktivovat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Projektová role byla deaktivována.",
            operation: () => _projectService.DeactivateProjectRoleAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AssignProjectSubsystem(AssignProjectSubsystemCommand command, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Subsystém nelze přiřadit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Subsystém byl přiřazen k projektu.",
            operation: () => _projectService.AssignProjectSubsystemAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateProjectSubsystem(DeactivateProjectSubsystemCommand command, int projektId, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: projektId,
            invalidAjaxMessage: "Subsystém projektu nelze deaktivovat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Subsystém projektu byl deaktivován.",
            operation: () => _projectService.DeactivateProjectSubsystemAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AssignProjectSubsystemRole(AssignProjectSubsystemRoleCommand command, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Subsystemovou roli nelze přiřadit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Role v subsystému byla přiřazena.",
            operation: () => _projectService.AssignProjectSubsystemRoleAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeactivateProjectSubsystemRole(DeactivateProjectSubsystemRoleCommand command, int projektId, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: projektId,
            invalidAjaxMessage: "Subsystemovou roli nelze deaktivovat.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Role v subsystému byla deaktivována.",
            operation: () => _projectService.DeactivateProjectSubsystemRoleAsync(command, CurrentUserContext, ct));
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

    private Task<IReadOnlyList<LookupOptionViewModel>> BuildProjectStatusOptionsAsync(CancellationToken ct = default)
        => _projectService.BuildProjectStatusOptionsAsync(CurrentUserContext, ct);

    private void PrepareProjectDetailPresentation(ProjektDetailViewModel model)
    {
        AttachCurrentUser(model);

        var projectId = model.Projekt.Id;
        var canManageRecords = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        var canManageSchedules = canManageRecords
            || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projectId)
            || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, projectId);

        model.CanCreateMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, projectId);
        model.CanEditMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, projectId);
        model.CanManageTeam = CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projectId);
        model.CanManageRecords = canManageRecords;
        model.CanManageSchedules = canManageSchedules;
        model.PageTitle = model.Projekt.Nazev;
        model.BackUrl = Url.Action("Index", "Projekty") ?? "/Projekty";
        model.BackLabel = "Zpět na přehled";
        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CreateRecordEditorUrl = Url.Action("Create", "Zaznamy", new { projektId = projectId }) ?? $"/Zaznamy/Create?projektId={projectId}";
        model.ReturnToProjectUrl = Url.Action("Detail", "Projekty", new { id = projectId, tab = "jednani" }) ?? $"/Projekty/Detail/{projectId}?tab=jednani";
        model.ProjectPrintUrl = Url.Action("ProjektTisk", "Export", new { projektId = projectId, autoPrint = true }) ?? $"/Export/Projekt/{projectId}/Tisk?autoPrint=true";
        model.ProjectWordUrl = Url.Action("ProjektWord", "Export", new { projektId = projectId }) ?? $"/Export/Projekt/{projectId}/Word";

        foreach (var record in model.Zaznamy)
        {
            record.CanEditRecord = canManageRecords;
            record.CanEditSchedule = record.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projectId);
            record.CanAddSchedule = record.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, projectId);
            record.CanManageSchedule = record.CanEditSchedule || record.CanAddSchedule;
            record.CanCommentAsSubsystemLeader = CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId)
                && record.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
            record.CanAddComment = record.CanEditRecord || record.CanCommentAsSubsystemLeader;
            record.EditButtonLabel = record.CanEditRecord ? "Upravit" : "GANTT";
            record.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        }

        foreach (var item in model.HarmonogramUkoly)
        {
            item.CanManageSchedule = canManageSchedules;
            item.ScheduleEditUrl = Url.Action("Edit", "Zaznamy", new { id = item.ZaznamId, projektId = projectId }) ?? $"/Zaznamy/Edit/{item.ZaznamId}";
        }
    }

    private Task<IActionResult> ExecuteTeamValidatedActionAsync(
        int projektId,
        string invalidAjaxMessage,
        string invalidFallbackMessage,
        string successMessage,
        Func<Task> operation)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId),
            invalidAjaxMessage: invalidAjaxMessage,
            invalidFallbackMessage: invalidFallbackMessage,
            onInvalidRedirect: () => RedirectToTeamTab(projektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToTeamTab(projektId)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(BuildTeamAjaxSuccess(projektId, successMessage)),
            operation: operation);
    }

    private Task<IActionResult> ExecuteTeamActionAsync(int projektId, Func<Task> operation)
    {
        return ExecuteCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToTeamTab(projektId)),
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
