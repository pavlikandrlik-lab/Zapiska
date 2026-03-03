using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Settings;

namespace PmTracker.Web.Controllers;

public sealed class NastaveniController : BaseController
{
    private readonly ISettingsService _settingsService;

    public NastaveniController(
        IPmTrackerDataStore dataStore,
        IUserContextResolver userContextResolver,
        ISettingsService settingsService)
        : base(dataStore, userContextResolver)
    {
        _settingsService = settingsService;
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

        var model = new AuthzRoleModalViewModel
        {
            Command = role is null
                ? new SaveAuthzRoleCommand()
                : new SaveAuthzRoleCommand
                {
                    Id = role.Id,
                    Kod = role.Kod,
                    Nazev = role.Nazev,
                    Popis = role.Popis
                },
            UserId = userId,
            ProjektId = projektId,
            IsEdit = role is not null
        };

        return View(model);
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

        var categoryId = permission is null
            ? panel.PermissionCategories.OrderBy(x => x.SortOrder).ThenBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase).FirstOrDefault()?.Id ?? 0
            : panel.PermissionCategories.FirstOrDefault(x => string.Equals(x.Kod, permission.CategoryKod, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        var permissionCatalog = PermissionKeys.BuildCatalog();
        var selectedCatalogEntry = permissionCatalog.FirstOrDefault(x =>
            string.Equals(x.Key, permission?.Klic, StringComparison.OrdinalIgnoreCase));
        if (selectedCatalogEntry is not null)
        {
            categoryId = panel.PermissionCategories
                .FirstOrDefault(x => string.Equals(x.Kod, selectedCatalogEntry.CategoryKod, StringComparison.OrdinalIgnoreCase))
                ?.Id ?? categoryId;
        }

        var model = new AuthzPermissionModalViewModel
        {
            Command = permission is null
                ? new SaveAuthzPermissionCommand
                {
                    CategoryId = categoryId,
                    ScopeLevel = selectedCatalogEntry?.ScopeLevel ?? "PROJECT"
                }
                : new SaveAuthzPermissionCommand
                {
                    Id = permission.Id,
                    Klic = permission.Klic,
                    Nazev = permission.Nazev,
                    CategoryId = categoryId,
                    ScopeLevel = selectedCatalogEntry?.ScopeLevel ?? permission.ScopeLevel
                },
            Categories = panel.PermissionCategories.OrderBy(x => x.SortOrder).ThenBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase).ToList(),
            AvailableKeys = BuildPermissionKeyOptions(permission?.Klic),
            PermissionCatalog = permissionCatalog,
            UserId = userId,
            ProjektId = projektId,
            IsEdit = permission is not null
        };

        return View(model);
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

        var roleOptions = panel.Role
            .Where(x => x.IsActive)
            .OrderBy(x => x.Kod, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(),
                Label = $"{x.Kod} - {x.Nazev}"
            })
            .ToList();

        var model = new UserRolesModalViewModel
        {
            Command = new SaveUserRolesForUserCommand
            {
                OsobaId = user.OsobaId,
                RoleIds = user.RoleAssignments.Select(x => x.RoleId).ToList()
            },
            OsobaLabel = user.Osoba,
            Email = user.Email,
            RoleOptions = roleOptions,
            UserId = userId,
            ProjektId = projektId
        };

        return View(model);
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

        var selectedRoleId = mapping?.RoleId ?? roleId ?? panel.Role.Where(x => x.IsActive).OrderBy(x => x.Kod).Select(x => (int?)x.Id).FirstOrDefault() ?? 0;
        var selectedPermissionId = mapping?.PermissionId ?? permissionId ?? panel.Permissions.Where(x => x.IsActive).OrderBy(x => x.Klic).Select(x => (int?)x.Id).FirstOrDefault() ?? 0;

        var model = new RolePermissionModalViewModel
        {
            Command = mapping is null
                ? new SaveRolePermissionCommand
                {
                    RoleId = selectedRoleId,
                    PermissionId = selectedPermissionId,
                    ScopeMode = "ALL",
                    IsAllowed = true
                }
                : new SaveRolePermissionCommand
                {
                    Id = mapping.Id,
                    RoleId = mapping.RoleId,
                    PermissionId = mapping.PermissionId,
                    ScopeMode = mapping.ScopeMode,
                    IsAllowed = mapping.IsAllowed,
                    ProjektIds = mapping.ProjektIds.ToList()
                },
            RoleOptions = panel.Role
                .Where(x => x.IsActive)
                .OrderBy(x => x.Kod, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new LookupOptionViewModel
                {
                    Value = x.Id.ToString(),
                    Label = $"{x.Kod} - {x.Nazev}"
                })
                .ToList(),
            PermissionOptions = panel.Permissions
                .Where(x => x.IsActive)
                .OrderBy(x => x.Klic, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new LookupOptionViewModel
                {
                    Value = x.Id.ToString(),
                    Label = $"{x.Klic} - {x.Nazev}"
                })
                .ToList(),
            ProjectOptions = panel.Projekty
                .OrderBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new LookupOptionViewModel
                {
                    Value = x.Id.ToString(),
                    Label = x.Nazev
                })
                .ToList(),
            UserId = userId,
            ProjektId = projektId,
            IsEdit = mapping is not null
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveRole(SaveAuthzRoleCommand command, int? userId, int? projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: "Roli nelze uložit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { section = "role", userId, projektId })!,
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "role", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("role", userId, projektId),
                message: "Role byla uložena."),
            operation: () => _settingsService.SaveAuthzRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleRole(ToggleAuthzRoleCommand command, int? userId, int? projektId)
    {
        return ExecuteCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "role", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("role", userId, projektId),
                message: "Stav role byl upraven."),
            operation: () => _settingsService.ToggleAuthzRole(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SavePermission(SaveAuthzPermissionCommand command, int? userId, int? projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: "Akci nelze uložit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { section = "akce", userId, projektId })!,
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "akce", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("akce", userId, projektId),
                message: "Akce byla uložena."),
            operation: () =>
            {
                ApplyPermissionCatalogDefaults(command);
                _settingsService.SaveAuthzPermission(command, CurrentUserContext);
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TogglePermission(ToggleAuthzPermissionCommand command, int? userId, int? projektId)
    {
        return ExecuteCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "akce", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("akce", userId, projektId),
                message: "Stav akce byl upraven."),
            operation: () => _settingsService.ToggleAuthzPermission(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveUserRole(SaveUserRoleAssignmentCommand command, int? userId, int? projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: "Přiřazení role nelze uložit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { section = "uzivatele-role", userId, projektId })!,
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "uzivatele-role", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("uzivatele-role", userId, projektId),
                message: "Přiřazení role bylo uloženo."),
            operation: () => _settingsService.SaveUserRoleAssignment(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveUserRolesForUser(SaveUserRolesForUserCommand command, int? userId, int? projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: "Role uživatele nelze uložit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { section = "uzivatele-role", userId, projektId })!,
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "uzivatele-role", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("uzivatele-role", userId, projektId),
                message: "Role uživatele byly uloženy."),
            operation: () => _settingsService.SaveUserRolesForUser(command, CurrentUserContext));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveRolePermission(SaveRolePermissionCommand command, int? userId, int? projektId)
    {
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: "Mapování role/akce nelze uložit.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { section = "role-akce", userId, projektId })!,
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "role-akce", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("role-akce", userId, projektId),
                message: "Mapování role/akce bylo uloženo."),
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
        return ExecuteValidatedCommand(
            hasPermission: () => CurrentUserContext.HasPermission(PermissionKeys.SettingsManage),
            invalidAjaxMessage: "Mapování role/akce nelze smazat.",
            invalidFallbackMessage: "Formulář obsahuje neplatné hodnoty.",
            onInvalidRedirect: () => RedirectToAction(nameof(Index), new { section = "role-akce", userId, projektId })!,
            onSuccessRedirect: () => RedirectToAction(nameof(Index), new { section = "role-akce", userId, projektId })!,
            onAjaxSuccess: () => AjaxSuccessResult(
                refreshScope: "nastaveni-panel",
                refreshUrl: BuildPanelRefreshUrl("role-akce", userId, projektId),
                message: "Mapování role/akce bylo smazáno."),
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

    private static IReadOnlyList<LookupOptionViewModel> BuildPermissionKeyOptions(string? selectedKey)
    {
        var options = PermissionKeys.BuildLookupOptions().ToList();
        if (!string.IsNullOrWhiteSpace(selectedKey) &&
            !options.Any(x => string.Equals(x.Value, selectedKey, StringComparison.OrdinalIgnoreCase)))
        {
            options.Insert(0, new LookupOptionViewModel
            {
                Value = selectedKey.Trim(),
                Label = $"{selectedKey.Trim()} - historický klíč (mimo aktuální kód)"
            });
        }

        return options;
    }

    private void ApplyPermissionCatalogDefaults(SaveAuthzPermissionCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Klic))
        {
            return;
        }

        var catalogEntry = PermissionKeys.BuildCatalog()
            .FirstOrDefault(x => string.Equals(x.Key, command.Klic.Trim(), StringComparison.OrdinalIgnoreCase));
        if (catalogEntry is null)
        {
            return;
        }

        var panel = _settingsService.BuildNastaveniPanel("akce", CurrentUserContext, null, null);
        var category = panel.PermissionCategories
            .FirstOrDefault(x => string.Equals(x.Kod, catalogEntry.CategoryKod, StringComparison.OrdinalIgnoreCase));

        if (category is not null)
        {
            command.CategoryId = category.Id;
        }

        command.ScopeLevel = catalogEntry.ScopeLevel;
    }
}
