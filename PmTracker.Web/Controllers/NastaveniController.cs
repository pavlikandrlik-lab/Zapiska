using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Web.Controllers;

[Authorize(Policy = "permission:settings.view")]
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
        var normalizedSection = NormalizeSection(section);
        if (normalizedSection == "efektivni-prava" && !CurrentUserContext.HasPermission(permissionKey: PermissionKeys.SettingsManage))
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
        var normalizedSection = NormalizeSection(section);
        if (normalizedSection == "efektivni-prava" && !CurrentUserContext.HasPermission(permissionKey: PermissionKeys.SettingsManage))
        {
            return Unauthorized();
        }

        var panel = await _settingsService.BuildNastaveniPanelAsync(normalizedSection, CurrentUserContext, userId, projektId, ct);
        PrepareSettingsPanelPresentation(panel);
        return PartialView("_DetailPanel", panel);
    }

    [HttpGet]
    [Authorize(Policy = "permission:settings.manage")]
    public async Task<IActionResult> UserRolesModal(int osobaId, int? userId, int? projektId, CancellationToken ct)
    {
        var panel = await _settingsService.BuildNastaveniPanelAsync("uzivatele-role", CurrentUserContext, userId, projektId, ct);
        var user = panel.UserRoles.FirstOrDefault(x => x.OsobaId == osobaId);
        if (user is null)
        {
            return NotFound();
        }

        return View(_settingsModalModelFactory.BuildUserRolesModal(user, panel.Role, userId, projektId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:settings.manage")]
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
    [Authorize(Policy = "permission:settings.manage")]
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

    private static string NormalizeSection(string? section)
    {
        var normalized = (section ?? "role").Trim().ToLowerInvariant();
        return normalized is "role" or "akce" or "role-akce" or "uzivatele-role" or "efektivni-prava" or "synchronizace"
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
            hasPermission: () => true,
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

    private RedirectToActionResult RedirectToSection(string section, int? userId, int? projektId)
        => RedirectToAction(nameof(Index), new { section, userId, projektId })!;

    private void PrepareSettingsPanelPresentation(NastaveniPanelViewModel panel)
    {
        AttachCurrentUser(panel);
        panel.PageTitle = panel.Nazev;
        panel.CanManageSettings = CurrentUserContext.HasPermission(permissionKey: PermissionKeys.SettingsManage);
    }

}
