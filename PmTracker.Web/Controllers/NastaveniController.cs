using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Web.Controllers;

public sealed class NastaveniController : BaseController
{
    private readonly ISettingsService _settingsService;
    private readonly ISettingsModalModelFactory _settingsModalModelFactory;

    public NastaveniController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        ISettingsService settingsService,
        ISettingsModalModelFactory settingsModalModelFactory)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _settingsService = settingsService;
        _settingsModalModelFactory = settingsModalModelFactory;
    }

    public async Task<IActionResult> Index(string? section, int? userId, int? projektId, CancellationToken ct)
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

        var model = AttachCurrentUser(await _settingsService.BuildNastaveniDashboardAsync(normalizedSection, CurrentUserContext, userId, projektId, ct));
        model.PageTitle = "Nastavení";
        PrepareSettingsPanelPresentation(model.AktivniPanel);
        return View(model);
    }

    public async Task<IActionResult> Panel(string? section, int? userId, int? projektId, CancellationToken ct)
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

        var panel = await _settingsService.BuildNastaveniPanelAsync(normalizedSection, CurrentUserContext, userId, projektId, ct);
        PrepareSettingsPanelPresentation(panel);
        return PartialView("_DetailPanel", panel);
    }

    [HttpGet]
    public async Task<IActionResult> UserRolesModal(int osobaId, int? userId, int? projektId, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = await _settingsService.BuildNastaveniPanelAsync("uzivatele-role", CurrentUserContext, userId, projektId, ct);
        var user = panel.UserRoles.FirstOrDefault(x => x.OsobaId == osobaId);
        if (user is null)
        {
            return NotFound();
        }

        return View(_settingsModalModelFactory.BuildUserRolesModal(user, panel.Role, userId, projektId));
    }

    [HttpGet]
    public async Task<IActionResult> RoleModal(int? id, int? userId, int? projektId, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = await _settingsService.BuildNastaveniPanelAsync("role", CurrentUserContext, userId, projektId, ct);
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
    public async Task<IActionResult> PermissionModal(int? id, int? userId, int? projektId, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = await _settingsService.BuildNastaveniPanelAsync("akce", CurrentUserContext, userId, projektId, ct);
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
    public async Task<IActionResult> RolePermissionModal(int? id, int? roleId, int? permissionId, int? userId, int? projektId, CancellationToken ct)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.SettingsManage))
        {
            return Forbid();
        }

        var panel = await _settingsService.BuildNastaveniPanelAsync("role-akce", CurrentUserContext, userId, projektId, ct);
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
    public Task<IActionResult> SaveRole(SaveAuthzRoleCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsValidatedActionAsync(
            section: "role",
            invalidAjaxMessage: "Roli nelze uložit.",
            successMessage: "Role byla uložena.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.SaveAuthzRoleAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ToggleRole(ToggleAuthzRoleCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsActionAsync(
            section: "role",
            successMessage: "Stav role byl upraven.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.ToggleAuthzRoleAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SavePermission(SaveAuthzPermissionCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsValidatedActionAsync(
            section: "akce",
            invalidAjaxMessage: "Akci nelze uložit.",
            successMessage: "Akce byla uložena.",
            userId: userId,
            projektId: projektId,
            operation: async () =>
            {
                var panel = await _settingsService.BuildNastaveniPanelAsync("akce", CurrentUserContext, null, null, ct);
                _settingsModalModelFactory.ApplyPermissionCatalogDefaults(command, panel.PermissionCategories);
                await _settingsService.SaveAuthzPermissionAsync(command, CurrentUserContext, ct);
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TogglePermission(ToggleAuthzPermissionCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsActionAsync(
            section: "akce",
            successMessage: "Stav akce byl upraven.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.ToggleAuthzPermissionAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveUserRole(SaveUserRoleAssignmentCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsValidatedActionAsync(
            section: "uzivatele-role",
            invalidAjaxMessage: "Přiřazení role nelze uložit.",
            successMessage: "Přiřazení role bylo uloženo.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.SaveUserRoleAssignmentAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveUserRolesForUser(SaveUserRolesForUserCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsValidatedActionAsync(
            section: "uzivatele-role",
            invalidAjaxMessage: "Role uživatele nelze uložit.",
            successMessage: "Role uživatele byly uloženy.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.SaveUserRolesForUserAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SaveRolePermission(SaveRolePermissionCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsValidatedActionAsync(
            section: "role-akce",
            invalidAjaxMessage: "Mapování role/akce nelze uložit.",
            successMessage: "Mapování role/akce bylo uloženo.",
            userId: userId,
            projektId: projektId,
            operation: async () =>
            {
                command.IsAllowed = true;
                await _settingsService.SaveRolePermissionAsync(command, CurrentUserContext, ct);
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteRolePermission(DeleteRolePermissionCommand command, int? userId, int? projektId, CancellationToken ct = default)
    {
        return ExecuteSettingsValidatedActionAsync(
            section: "role-akce",
            invalidAjaxMessage: "Mapování role/akce nelze smazat.",
            successMessage: "Mapování role/akce bylo smazáno.",
            userId: userId,
            projektId: projektId,
            operation: () => _settingsService.DeleteRolePermissionAsync(command, CurrentUserContext, ct));
    }

    private static string NormalizeSection(string? section)
    {
        var normalized = (section ?? "role").Trim().ToLowerInvariant();
        return normalized is "role" or "akce" or "role-akce" or "uzivatele-role" or "efektivni-prava"
            ? normalized
            : "role";
    }

    private string BuildPanelRefreshUrl(string section, int? userId, int? projektId)
    {
        return Url.Action(nameof(Panel), new { section, userId, projektId }) ?? Url.Action(nameof(Panel), new { section })!;
    }

    private Task<IActionResult> ExecuteSettingsValidatedActionAsync(
        string section,
        string invalidAjaxMessage,
        string successMessage,
        int? userId,
        int? projektId,
        Func<Task> operation)
    {
        return ExecuteValidatedCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: invalidAjaxMessage,
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToSection(section, userId, projektId),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToSection(section, userId, projektId)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl(section, userId, projektId),
                message: successMessage)),
            operation: operation);
    }

    private Task<IActionResult> ExecuteSettingsActionAsync(
        string section,
        string successMessage,
        int? userId,
        int? projektId,
        Func<Task> operation)
    {
        return ExecuteCommandAsync(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToSection(section, userId, projektId)),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl(section, userId, projektId),
                message: successMessage)),
            operation: operation);
    }

    private RedirectToActionResult RedirectToSection(string section, int? userId, int? projektId)
        => RedirectToAction(nameof(Index), new { section, userId, projektId })!;

    private void PrepareSettingsPanelPresentation(NastaveniPanelViewModel panel)
    {
        AttachCurrentUser(panel);
        panel.PageTitle = panel.Nazev;
        panel.CanManageSettings = CurrentUserContext.HasPermission(PermissionKeys.SettingsManage);
    }

}
