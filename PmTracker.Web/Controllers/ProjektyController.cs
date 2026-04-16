using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;
using System.Globalization;

namespace PmTracker.Web.Controllers;

public sealed class ProjektyController : BaseController
{
    private const string RecordsTab = "zaznamy";
    private const string ScheduleTab = "harmonogram";
    private const string MeetingsTab = "jednani";
    private const string TeamTab = "tym";
    private const string ProposalsTab = "navrhy";
    private const string TeamRefreshScope = "projekty-detail-tym";
    private const string ProposalsRefreshScope = "projekty-detail-navrhy";

    private readonly IProjectService _projectService;
    private readonly IMeetingService _meetingService;
    private readonly IRecordProposalService _recordProposalService;
    private readonly IProjectDashboardService _projectDashboardService;

    public ProjektyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IProjectService projectService,
        IMeetingService meetingService,
        IRecordProposalService recordProposalService,
        IProjectDashboardService projectDashboardService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _projectService = projectService;
        _meetingService = meetingService;
        _recordProposalService = recordProposalService;
        _projectDashboardService = projectDashboardService;
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

    public async Task<IActionResult> Detail(int id, string? tab, int? recordId, bool openComments = false, CancellationToken ct = default)
    {
        if (!await _projectService.ProjektExistsAsync(id, ct))
        {
            return RedirectToAction(nameof(Index));
        }

        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var requestedTab = NormalizeProjectTab(tab);
        var model = await _projectService.BuildProjektDetailAsync(id, ct);
        model.ActiveTab = requestedTab;
        model.TargetRecordId = requestedTab == RecordsTab ? recordId : null;
        model.TargetRecordOpenComments = requestedTab == RecordsTab && openComments;
        await PrepareProjectDetailPresentationAsync(model, ct);
        await PrepareActiveProjectTabAsync(model, requestedTab, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> RecordsTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectRecordsTabAsync(id, ct);
        await PrepareProjectRecordsTabPresentationAsync(model, ct);
        return PartialView("~/Views/Projekty/_ProjectRecordsTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> RecordMeetingCommentStates(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var statesByRecordId = await _projectService.BuildRecordMeetingCommentStatesAsync(id, ct);
        return Json(new ProjektMeetingCommentStatesResponseViewModel
        {
            StatesByRecordId = statesByRecordId.ToDictionary(
                item => item.Key.ToString(CultureInfo.InvariantCulture),
                item => item.Value)
        });
    }

    [HttpGet]
    public async Task<IActionResult> SearchProjectMemberCandidates(int id, [FromQuery(Name = "q")] string? query, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, id))
        {
            return Forbid();
        }

        var results = await _projectService.SearchProjectMemberCandidatesAsync(query ?? string.Empty, ct);
        return Json(new PersonPickerSearchResponseViewModel
        {
            Results = results
        });
    }

    [HttpGet]
    public async Task<IActionResult> HarmonogramTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectScheduleTabAsync(id, ct);
        PrepareProjectScheduleTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectScheduleTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> JednaniTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectMeetingsTabAsync(id, ct);
        PrepareProjectMeetingsTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectMeetingsTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> TymTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectTeamTabAsync(id, ct);
        PrepareProjectTeamTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectTeamTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> NavrhyTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        if (!await _recordProposalService.CanViewProposalTabAsync(id, CurrentUserContext, ct))
        {
            return Forbid();
        }

        var model = await _recordProposalService.BuildProjectProposalsTabAsync(id, CurrentUserContext, ct);
        PrepareProjectProposalsTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectProposalsTab.cshtml", model);
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

        var project = await _projectService.GetProjectListItemAsync(id, ct);
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

        var project = await _projectService.GetProjectListItemAsync(id, ct);
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

        var model = await _meetingService.BuildNewMeetingModalAsync(projektId, GetLocalNow(), ct);
        return View("NewMeetingModal", model);
    }

    [HttpGet]
    public async Task<IActionResult> EditMeetingModal(int projektId, int meetingId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, projektId))
        {
            return Forbid();
        }

        var isEditable = await _meetingService.IsMeetingEditableAsync(projektId, meetingId, ct);
        if (!isEditable.HasValue)
        {
            return NotFound();
        }

        if (!isEditable.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var model = await _meetingService.BuildEditMeetingModalAsync(projektId, meetingId, ct);
        if (model is null)
        {
            return NotFound();
        }

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

        var modalOptions = await _projectService.BuildProjectTeamModalOptionsAsync(projektId, ct);
        var defaultRole = modalOptions.RoleProjektu.FirstOrDefault()?.Value ?? string.Empty;
        var model = new TeamMemberModalViewModel
        {
            Title = "Přidat člena týmu",
            Command = new SaveTeamMemberCommand
            {
                ProjektId = projektId,
                Role = defaultRole
            },
            SearchUrl = Url.Action(nameof(SearchProjectMemberCandidates), new { id = projektId }),
            DostupniClenoveTymu = [],
            RoleProjektu = modalOptions.RoleProjektu
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

        var modalOptions = await _projectService.BuildProjectTeamModalOptionsAsync(projektId, ct);
        return View("AssignProjectRoleModal", new AssignProjectRoleModalViewModel
        {
            Title = "Přidat projektovou roli",
            Command = new AssignProjectRoleCommand { ProjektId = projektId },
            SearchUrl = Url.Action(nameof(SearchProjectMemberCandidates), new { id = projektId }),
            DostupneOsoby = [],
            RoleProjektu = modalOptions.RoleProjektu
        });
    }

    [HttpGet]
    public async Task<IActionResult> AssignProjectSubsystemModal(int projektId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var modalOptions = await _projectService.BuildProjectTeamModalOptionsAsync(projektId, ct);
        return View("AssignProjectSubsystemModal", new AssignProjectSubsystemModalViewModel
        {
            Title = "Přiřadit subsystém projektu",
            Command = new AssignProjectSubsystemCommand { ProjektId = projektId },
            Subsystemy = modalOptions.DostupneSubsystemy
        });
    }

    [HttpGet]
    public async Task<IActionResult> AssignProjectSubsystemRoleModal(int projektId, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, projektId))
        {
            return Forbid();
        }

        var modalOptions = await _projectService.BuildProjectTeamModalOptionsAsync(projektId, ct);
        return View("AssignProjectSubsystemRoleModal", new AssignProjectSubsystemRoleModalViewModel
        {
            Title = "Přidat roli v subsystému",
            Command = new AssignProjectSubsystemRoleCommand { ProjektId = projektId },
            ProjektSubsystemy = modalOptions.DostupneProjektoveSubsystemy,
            SearchUrl = Url.Action(nameof(SearchProjectMemberCandidates), new { id = projektId }),
            DostupneOsoby = [],
            RoleSubsystemu = modalOptions.RoleSubsystemu
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

            var isEditable = await _meetingService.IsMeetingEditableAsync(command.ProjektId, command.Id.Value, ct);
            if (!isEditable.HasValue)
            {
                return NotFound();
            }

            if (!isEditable.Value)
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
                refreshUrl: Url.Action(nameof(JednaniTabPartial), "Projekty", new { id = command.ProjektId }),
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
                refreshUrl: Url.Action(nameof(JednaniTabPartial), "Projekty", new { id = command.ProjektId }),
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
    public Task<IActionResult> ReorderProjectSubsystem(ReorderProjectSubsystemCommand command, CancellationToken ct = default)
    {
        return ExecuteTeamValidatedActionAsync(
            projektId: command.ProjektId,
            invalidAjaxMessage: "Pořadí subsystému projektu nelze změnit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            successMessage: "Pořadí subsystémů bylo upraveno.",
            operation: () => _projectService.ReorderProjectSubsystemAsync(command, CurrentUserContext, ct));
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

    private Task<IReadOnlyList<LookupOptionViewModel>> BuildProjectStatusOptionsAsync(CancellationToken ct = default)
        => _projectService.BuildProjectStatusOptionsAsync(CurrentUserContext, ct);

    private async Task PrepareProjectDetailPresentationAsync(ProjektDetailViewModel model, CancellationToken ct)
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
        model.CanViewProposals = await _recordProposalService.CanViewProposalTabAsync(projectId, CurrentUserContext, ct);
        model.CanViewDashboard = CurrentUserContext.IsSuperAdmin
            || await _projectDashboardService.CanAccessDashboardAsync(projectId, CurrentUserContext.OsobaId, ct);
        model.PageTitle = model.Projekt.Nazev;
        model.BackUrl = Url.Action("Index", "Projekty") ?? "/Projekty";
        model.BackLabel = "Zpět na přehled";
        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CreateRecordEditorUrl = Url.Action("Create", "Zaznamy", new { projektId = projectId }) ?? $"/Zaznamy/Create?projektId={projectId}";
        model.ReturnToProjectUrl = Url.Action("Detail", "Projekty", new { id = projectId, tab = "jednani" }) ?? $"/Projekty/Detail/{projectId}?tab=jednani";
        model.ProjectPrintUrl = Url.Action("ProjektTisk", "Export", new { projektId = projectId, autoPrint = true }) ?? $"/Export/Projekt/{projectId}/Tisk?autoPrint=true";
        model.ProjectWordUrl = Url.Action("ProjektWord", "Export", new { projektId = projectId }) ?? $"/Export/Projekt/{projectId}/Word";
        model.HarmonogramTab.LoadUrl = Url.Action(nameof(HarmonogramTabPartial), new { id = projectId }) ?? $"/Projekty/HarmonogramTabPartial/{projectId}";
        model.JednaniTab.LoadUrl = Url.Action(nameof(JednaniTabPartial), new { id = projectId }) ?? $"/Projekty/JednaniTabPartial/{projectId}";
        model.TymTab.LoadUrl = Url.Action(nameof(TymTabPartial), new { id = projectId }) ?? $"/Projekty/TymTabPartial/{projectId}";
        model.NavrhyTab.LoadUrl = Url.Action(nameof(NavrhyTabPartial), new { id = projectId }) ?? $"/Projekty/NavrhyTabPartial/{projectId}";

        await PrepareProjectRecordsTabPresentationAsync(model.ZaznamyTab, ct);
    }

    private async Task PrepareActiveProjectTabAsync(ProjektDetailViewModel model, string requestedTab, CancellationToken ct)
    {
        if (string.Equals(requestedTab, ScheduleTab, StringComparison.OrdinalIgnoreCase))
        {
            var scheduleTab = await _projectService.BuildProjectScheduleTabAsync(model.Projekt.Id, ct);
            PrepareProjectScheduleTabPresentation(scheduleTab);
            model.LoadedHarmonogramTab = scheduleTab;
            return;
        }

        if (string.Equals(requestedTab, MeetingsTab, StringComparison.OrdinalIgnoreCase))
        {
            var meetingsTab = await _projectService.BuildProjectMeetingsTabAsync(model.Projekt.Id, ct);
            PrepareProjectMeetingsTabPresentation(meetingsTab);
            model.LoadedJednaniTab = meetingsTab;
            return;
        }

        if (string.Equals(requestedTab, TeamTab, StringComparison.OrdinalIgnoreCase))
        {
            var teamTab = await _projectService.BuildProjectTeamTabAsync(model.Projekt.Id, ct);
            PrepareProjectTeamTabPresentation(teamTab);
            model.LoadedTymTab = teamTab;
            return;
        }

        if (string.Equals(requestedTab, ProposalsTab, StringComparison.OrdinalIgnoreCase)
            && await _recordProposalService.CanViewProposalTabAsync(model.Projekt.Id, CurrentUserContext, ct))
        {
            var proposalsTab = await _recordProposalService.BuildProjectProposalsTabAsync(model.Projekt.Id, CurrentUserContext, ct);
            PrepareProjectProposalsTabPresentation(proposalsTab);
            model.LoadedNavrhyTab = proposalsTab;
        }
    }

    private static string NormalizeProjectTab(string? tab)
    {
        if (string.Equals(tab, "gant", StringComparison.OrdinalIgnoreCase))
        {
            return ScheduleTab;
        }

        if (string.Equals(tab, ScheduleTab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tab, MeetingsTab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tab, TeamTab, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tab, ProposalsTab, StringComparison.OrdinalIgnoreCase))
        {
            return tab!.ToLowerInvariant();
        }

        return RecordsTab;
    }

    private async Task PrepareProjectRecordsTabPresentationAsync(ProjektZaznamyTabViewModel model, CancellationToken ct)
    {
        var projectId = model.ProjektId;
        var canManageRecords = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        var canCreateRecordProposal = !canManageRecords && CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId);

        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        model.CanManageRecords = canManageRecords;
        model.CanCreateRecordProposal = canCreateRecordProposal;
        model.CreateRecordEditorUrl = Url.Action("Create", "Zaznamy", new { projektId = projectId }) ?? $"/Zaznamy/Create?projektId={projectId}";
        model.CreateRecordProposalUrl = Url.Action("CreateRecordProposal", "Navrhy", new { projektId = projectId }) ?? $"/Navrhy/CreateRecordProposal?projektId={projectId}";
        model.RefreshUrl = Url.Action(nameof(RecordsTabPartial), new { id = projectId }) ?? $"/Projekty/RecordsTabPartial/{projectId}";
        model.MeetingCommentStatesUrl = Url.Action(nameof(RecordMeetingCommentStates), new { id = projectId }) ?? $"/Projekty/RecordMeetingCommentStates/{projectId}";
        model.ProjectPrintUrl = Url.Action("ProjektTisk", "Export", new { projektId = projectId, autoPrint = true }) ?? $"/Export/Projekt/{projectId}/Tisk?autoPrint=true";
        model.ProjectWordUrl = Url.Action("ProjektWord", "Export", new { projektId = projectId }) ?? $"/Export/Projekt/{projectId}/Word";

        foreach (var record in model.Zaznamy)
        {
            PrepareRecordCardShellPresentation(record, projectId);
        }

        foreach (var group in model.SkupinyZaznamu)
        {
            foreach (var record in group.Zaznamy)
            {
                PrepareRecordCardShellPresentation(record, projectId);
            }
        }
    }

    private void PrepareRecordCardShellPresentation(ProjektZaznamCardShellViewModel record, int projectId)
    {
        var summary = record.Summary;
        summary.CanEditRecord = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, projectId);
        summary.CanEditSchedule = summary.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, projectId);
        summary.CanAddSchedule = summary.JeUkol && CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, projectId);
        summary.CanManageSchedule = summary.CanEditSchedule || summary.CanAddSchedule;
        summary.CanCommentAsSubsystemLeader = CurrentUserContext.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId)
            && summary.AktualniSubsystemLeadEquivalentOsobaIds.Contains(CurrentUserContext.OsobaId);
        summary.CanAddComment = summary.CanEditRecord || summary.CanCommentAsSubsystemLeader;
        summary.CanCreateScheduleProposal = summary.JeUkol && summary.CanCommentAsSubsystemLeader;
        summary.EditButtonLabel = summary.CanEditRecord ? "Upravit" : "GANTT";
        summary.CurrentUserOsobaId = CurrentUserContext.OsobaId;
        record.DetailUrl = Url.Action("RecordDetailPartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Zaznamy/RecordDetailPartial?projektId={projectId}&zaznamId={summary.Id}";
        record.CommentsUrl = Url.Action("RecordCommentsPartial", "Zaznamy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={summary.Id}";
        summary.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy", new { projektId = projectId, zaznamId = summary.Id }) ?? $"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={summary.Id}";
    }

    private void PrepareProjectScheduleTabPresentation(ProjektHarmonogramTabViewModel model)
    {
        model.CurrentUserOsobaId = CurrentUserContext.OsobaId;

        var canManageSchedules = CurrentUserContext.HasPermission(PermissionKeys.RecordsEdit, model.ProjektId)
            || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleEdit, model.ProjektId)
            || CurrentUserContext.HasPermission(PermissionKeys.RecordsScheduleAdd, model.ProjektId);

        foreach (var item in model.HarmonogramUkoly)
        {
            item.CanManageSchedule = canManageSchedules;
            item.ScheduleEditUrl = Url.Action("Edit", "Zaznamy", new { id = item.ZaznamId, projektId = model.ProjektId }) ?? $"/Zaznamy/Edit/{item.ZaznamId}";
            item.ScheduleProposalUrl = Url.Action("CreateScheduleProposal", "Navrhy", new { projektId = model.ProjektId, zaznamId = item.ZaznamId }) ?? $"/Navrhy/CreateScheduleProposal?projektId={model.ProjektId}&zaznamId={item.ZaznamId}";
        }
    }

    private void PrepareProjectMeetingsTabPresentation(ProjektJednaniTabViewModel model)
    {
        model.CanCreateMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, model.ProjektId);
        model.CanEditMeetings = CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, model.ProjektId);
        model.ReturnToProjectUrl = Url.Action(nameof(Detail), new { id = model.ProjektId, tab = "jednani" }) ?? $"/Projekty/Detail/{model.ProjektId}?tab=jednani";
        model.PreviewRok = MeetingYearGroupBuilder.ResolvePreviewYear(model.RocniSkupiny, GetLocalNow().Year);
    }

    private void PrepareProjectTeamTabPresentation(ProjektTymTabViewModel model)
    {
        model.CanManageTeam = CurrentUserContext.HasPermission(PermissionKeys.TeamManage, model.ProjektId);
    }

    private void PrepareProjectProposalsTabPresentation(ProjektNavrhyTabViewModel model)
    {
        model.CreateRecordProposalUrl = Url.Action("CreateRecordProposal", "Navrhy", new { projektId = model.ProjektId }) ?? $"/Navrhy/CreateRecordProposal?projektId={model.ProjektId}";

        foreach (var item in model.NavrhyZalozeni.Concat(model.NavrhyHarmonogramu))
        {
            item.DetailUrl = Url.Action("ProposalDetail", "Navrhy", new { projektId = model.ProjektId, proposalId = item.Id }) ?? $"/Navrhy/ProposalDetail?projektId={model.ProjektId}&proposalId={item.Id}";
            item.PrefillCreateFormUrl = Url.Action("PrefillCreateProposal", "Navrhy", new { projektId = model.ProjektId, proposalId = item.Id }) ?? $"/Navrhy/PrefillCreateProposal?projektId={model.ProjektId}&proposalId={item.Id}";
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
            refreshUrl: Url.Action(nameof(TymTabPartial), new { id = projektId }),
            projectId: projektId,
            tab: TeamTab,
            message: successMessage);
    }

    private RedirectToActionResult RedirectToTeamTab(int projektId)
        => RedirectToAction(nameof(Detail), new { id = projektId, tab = TeamTab })!;
}
