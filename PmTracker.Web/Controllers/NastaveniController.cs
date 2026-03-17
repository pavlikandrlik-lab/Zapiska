using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Settings;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Web.Controllers;

public sealed class NastaveniController : BaseController
{
    private readonly ISettingsService _settingsService;
    private readonly ISettingsModalModelFactory _settingsModalModelFactory;

    public NastaveniController(
        IUserContextResolver userContextResolver,
        ISettingsService settingsService,
        ISettingsModalModelFactory settingsModalModelFactory)
        : base(userContextResolver)
    {
        _settingsService = settingsService;
        _settingsModalModelFactory = settingsModalModelFactory;
    }

    public IActionResult Index(string? section, int? userId, int? projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsView))
        {
            return RedirectToAction("Index", "Projekty");
        }

        var normalizedSection = NormalizeSection(section);
        if (normalizedSection == "efektivni-prava" && !CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return RedirectToAction(nameof(Index), new { section = "role" });
        }

        var model = _settingsService.BuildNastaveniDashboard(normalizedSection, CurrentUserContext, userId, projektId);
        return View(model);
    }

    public IActionResult Panel(string? section, int? userId, int? projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsView))
        {
            return Unauthorized();
        }

        var normalizedSection = NormalizeSection(section);
        if (normalizedSection == "efektivni-prava" && !CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Unauthorized();
        }

        var panel = _settingsService.BuildNastaveniPanel(normalizedSection, CurrentUserContext, userId, projektId);
        return PartialView("_DetailPanel", panel);
    }

    [HttpGet]
    public IActionResult RoleModal(int? id, int? userId, int? projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = _settingsService.BuildNastaveniPanel("role", CurrentUserContext, userId, projektId);
        var role = id.HasValue
            ? panel.Role.FirstOrDefault(x => x.Id == id.Value)
            : null;

        if (id.HasValue && role is null)
        {
            return NotFound();
        }

        if (role?.IsSystem == true)
        {
            return Forbid();
        }

        return View(_settingsModalModelFactory.BuildRoleModal(role, userId, projektId));
    }

    [HttpGet]
    public IActionResult PermissionModal(int? id, int? userId, int? projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = _settingsService.BuildNastaveniPanel("akce", CurrentUserContext, userId, projektId);
        var permission = id.HasValue
            ? panel.Permissions.FirstOrDefault(x => x.Id == id.Value)
            : null;

        if (id.HasValue && permission is null)
        {
            return NotFound();
        }

        if (permission?.IsSystem == true)
        {
            return Forbid();
        }

        return View(_settingsModalModelFactory.BuildPermissionModal(panel, permission, userId, projektId));
    }

    [HttpGet]
    public IActionResult UserRolesModal(int osobaId, int? userId, int? projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = _settingsService.BuildNastaveniPanel("uzivatele-role", CurrentUserContext, userId, projektId);
        var user = panel.UserRoles.FirstOrDefault(x => x.OsobaId == osobaId);
        if (user is null)
        {
            return NotFound();
        }

        return View(_settingsModalModelFactory.BuildUserRolesModal(user, panel.Role, userId, projektId));
    }

    [HttpGet]
    public IActionResult RolePermissionModal(int? id, int? roleId, int? permissionId, int? userId, int? projektId)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = _settingsService.BuildNastaveniPanel("role-akce", CurrentUserContext, userId, projektId);
        var mapping = id.HasValue
            ? panel.RolePermissionScopes.FirstOrDefault(x => x.Id == id.Value)
            : null;

        if (id.HasValue && mapping is null)
        {
            return NotFound();
        }

        if (mapping is not null && !mapping.IsAllowed)
        {
            return Forbid();
        }

        return View(_settingsModalModelFactory.BuildRolePermissionModal(panel, mapping, roleId, permissionId, userId, projektId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveRole(SaveAuthzRoleCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsValidatedAction(
            section: "role",
            invalidAjaxMessage: "Roli nelze uložit.",
            successMessage: "Role byla uložena.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.SaveAuthzRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleRole(ToggleAuthzRoleCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsAction(
            section: "role",
            successMessage: "Stav role byl upraven.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.ToggleAuthzRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SavePermission(SaveAuthzPermissionCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsValidatedAction(
            section: "akce",
            invalidAjaxMessage: "Akci nelze uložit.",
            successMessage: "Akce byla uložena.",
            userId: userId,
            projektId: projektId,
            operation: () =>
            {
                var panel = _settingsService.BuildNastaveniPanel("akce", CurrentUserContext, null, null);
                _settingsModalModelFactory.ApplyPermissionCatalogDefaults(command, panel.PermissionCategories);
                _settingsService.SaveAuthzPermission(command, CurrentUserContext);
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TogglePermission(ToggleAuthzPermissionCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsAction(
            section: "akce",
            successMessage: "Stav akce byl upraven.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.ToggleAuthzPermission(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveUserRole(SaveUserRoleAssignmentCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsValidatedAction(
            section: "uzivatele-role",
            invalidAjaxMessage: "Přiřazení role nelze uložit.",
            successMessage: "Přiřazení role bylo uloženo.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.SaveUserRoleAssignment(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveUserRolesForUser(SaveUserRolesForUserCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsValidatedAction(
            section: "uzivatele-role",
            invalidAjaxMessage: "Role uživatele nelze uložit.",
            successMessage: "Role uživatele byly uloženy.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.SaveUserRolesForUser(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveRolePermission(SaveRolePermissionCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsValidatedAction(
            section: "role-akce",
            invalidAjaxMessage: "Mapování role/akce nelze uložit.",
            successMessage: "Mapování role/akce bylo uloženo.",
            userId: userId,
            projektId: projektId,
            operation: () =>
            {
                command.IsAllowed = true;
                _settingsService.SaveRolePermission(command, CurrentUserContext);
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteRolePermission(DeleteRolePermissionCommand command, int? userId, int? projektId)
    {
        return ExecuteSettingsValidatedAction(
            section: "role-akce",
            invalidAjaxMessage: "Mapování role/akce nelze smazat.",
            successMessage: "Mapování role/akce bylo smazáno.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.DeleteRolePermission(command, CurrentUserContext));
    }

    private static string NormalizeSection(string? section)
    {
        return (section ?? "role").Trim().ToLowerInvariant();
    }

    private string BuildPanelRefreshUrl(string section, int? userId, int? projektId)
    {
        return Url.Action(nameof(Panel), new { section, userId, projektId }) ?? Url.Action(nameof(Panel), new { section })!;
    }

    private IActionResult ExecuteSettingsValidatedAction(
        string section,
        string invalidAjaxMessage,
        string successMessage,
        int? userId,
        int? projektId,
        Action operation)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: invalidAjaxMessage,
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToSection(section, userId, projektId),
            onSuccessRedirect: () => RedirectToSection(section, userId, projektId),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl(section, userId, projektId),
                message: successMessage),
            operation: operation);
    }

    private IActionResult ExecuteSettingsAction(
        string section,
        string successMessage,
        int? userId,
        int? projektId,
        Action operation)
    {
        return ExecuteCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            onSuccessRedirect: () => RedirectToSection(section, userId, projektId),
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl(section, userId, projektId),
                message: successMessage),
            operation: operation);
    }

    private RedirectToActionResult RedirectToSection(string section, int? userId, int? projektId)
        => RedirectToAction(nameof(Index), new { section, userId, projektId })!;

}
