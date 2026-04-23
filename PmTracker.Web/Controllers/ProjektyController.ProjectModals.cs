using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Controllers;

public sealed partial class ProjektyController
{
    [HttpGet]
    [Authorize(Policy = "permission:projects.create")]
    public async Task<IActionResult> NewProjectModal(CancellationToken ct = default)
    {
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
    [Authorize(Policy = "permission:projects.edit")]
    public async Task<IActionResult> EditProjectModal(int id, CancellationToken ct = default)
    {
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
                PouzivatIdentJednani = project.PouzivatIdentJednani,
                MistoPlneni = project.MistoPlneni,
                CisloRamcoveSmlouvy = project.CisloRamcoveSmlouvy
            },
            StavyProjektu = statusOptions
        };

        return View("ProjectModal", model);
    }

    [HttpGet]
    [Authorize(Policy = "permission:projects.delete")]
    public async Task<IActionResult> DeleteProjectModal(int id, CancellationToken ct = default)
    {
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
    [Authorize(Policy = "permission:team.member.add")]
    public async Task<IActionResult> AddTeamMemberModal(int projektId, CancellationToken ct = default)
    {
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
    [Authorize(Policy = "permission:team.role.assign")]
    public async Task<IActionResult> AssignProjectRoleModal(int projektId, CancellationToken ct = default)
    {
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
    [Authorize(Policy = "permission:team.subsystem.create")]
    public async Task<IActionResult> AssignProjectSubsystemModal(int projektId, CancellationToken ct = default)
    {
        var modalOptions = await _projectService.BuildProjectTeamModalOptionsAsync(projektId, ct);
        return View("AssignProjectSubsystemModal", new AssignProjectSubsystemModalViewModel
        {
            Title = "Přiřadit subsystém projektu",
            Command = new AssignProjectSubsystemCommand { ProjektId = projektId },
            Subsystemy = modalOptions.DostupneSubsystemy
        });
    }

    [HttpGet]
    [Authorize(Policy = "permission:team.subsystem.role.assign")]
    public async Task<IActionResult> AssignProjectSubsystemRoleModal(int projektId, CancellationToken ct = default)
    {
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
}
