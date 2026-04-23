using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed partial class ProjektyController
{
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
    [Authorize(Policy = "permission:projects.delete")]
    public Task<IActionResult> DeleteProject(SoftDeleteProjectCommand command, CancellationToken ct = default)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => true,
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

    // Meeting akce (Save, Delete, NewMeetingModal, EditMeetingModal) přesunuty do
    // JednaniController.Commands/Modals 2026-04-23. URL namespace dříve /Projekty/SaveMeeting
    // nyní /Jednani/Save atd. — změnou primárního endpointu. Žádná backward-compat route
    // není potřebná (interní UI forms, ne public bookmarks).

    // H-1 IDOR: projektId pochází z form body (command.ProjektId nebo query parametr),
    // ne z route. PermissionAuthorizationHandler čte projektId z RouteValues → pro tyto
    // akce by policy degradovala na global-only check. Per-project check provádí
    // ExecuteTeamValidatedActionAsync/ExecuteTeamActionAsync v body (viz níže).
    // Class-level [Authorize] dále vynucuje authenticated uživatele.
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

    // H-1 IDOR fix: per-project kontrola je provedena zde v body.
    // PermissionAuthorizationHandler čte projektId z RouteValues — ale team-management
    // akce dostávají projektId z form body (command.ProjektId) nebo query parametru.
    // Bez této kontroly by uživatel s team.manage na projektu A mohl upravovat projekt B.
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
