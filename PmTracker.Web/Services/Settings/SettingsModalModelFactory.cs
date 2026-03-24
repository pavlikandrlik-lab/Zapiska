using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Settings;

public sealed class SettingsModalModelFactory : ISettingsModalModelFactory
{
    public AuthzRoleModalViewModel BuildRoleModal(RoleViewModel? role, int? userId, int? projektId)
    {
        return new AuthzRoleModalViewModel
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
    }

    public AuthzPermissionModalViewModel BuildPermissionModal(NastaveniPanelViewModel panel, PermissionViewModel? permission, int? userId, int? projektId)
    {
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

        return new AuthzPermissionModalViewModel
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
            Categories = panel.PermissionCategories
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Nazev, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            AvailableKeys = BuildPermissionKeyOptions(permission?.Klic),
            PermissionCatalog = permissionCatalog,
            UserId = userId,
            ProjektId = projektId,
            IsEdit = permission is not null
        };
    }

    public UserRolesModalViewModel BuildUserRolesModal(UserRoleAssignmentViewModel user, IReadOnlyList<RoleViewModel> roles, int? userId, int? projektId)
    {
        var roleOptions = roles
            .Where(x => x.IsActive)
            .OrderBy(x => x.Kod, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new LookupOptionViewModel
            {
                Value = x.Id.ToString(),
                Label = $"{x.Kod} - {x.Nazev}"
            })
            .ToList();

        return new UserRolesModalViewModel
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
    }

    public RolePermissionModalViewModel BuildRolePermissionModal(
        NastaveniPanelViewModel panel,
        RolePermissionScopeViewModel? mapping,
        int? roleId,
        int? permissionId,
        int? userId,
        int? projektId)
    {
        var selectedRoleId = mapping?.RoleId ?? roleId ?? panel.Role.Where(x => x.IsActive).OrderBy(x => x.Kod).Select(x => (int?)x.Id).FirstOrDefault() ?? 0;
        var selectedPermissionId = mapping?.PermissionId ?? permissionId ?? panel.Permissions.Where(x => x.IsActive).OrderBy(x => x.Klic).Select(x => (int?)x.Id).FirstOrDefault() ?? 0;

        return new RolePermissionModalViewModel
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
    }

    public void ApplyPermissionCatalogDefaults(SaveAuthzPermissionCommand command, IReadOnlyList<PermissionCategoryViewModel> permissionCategories)
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

        var category = permissionCategories
            .FirstOrDefault(x => string.Equals(x.Kod, catalogEntry.CategoryKod, StringComparison.OrdinalIgnoreCase));

        if (category is not null)
        {
            command.CategoryId = category.Id;
        }

        command.ScopeLevel = catalogEntry.ScopeLevel;
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
}
