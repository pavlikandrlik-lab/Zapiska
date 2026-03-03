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
            .Where(project => CurrentUserContext.CanReadProject(project.Id))
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

        if (!CurrentUserContext.CanReadProject(id))
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
            DostupniClenoveTymu = detail.DostupniClenoveTymu,
            RoleProjektu = detail.RoleProjektu
        };

        return View(model);
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
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, command.ProjektId),
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
