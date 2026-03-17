using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Modules.Settings;

public interface ISettingsModalModelFactory
{
    AuthzRoleModalViewModel BuildRoleModal(RoleViewModel? role, int? userId, int? projektId);
    AuthzPermissionModalViewModel BuildPermissionModal(NastaveniPanelViewModel panel, PermissionViewModel? permission, int? userId, int? projektId);
    UserRolesModalViewModel BuildUserRolesModal(UserRoleAssignmentViewModel user, IReadOnlyList<RoleViewModel> roles, int? userId, int? projektId);
    RolePermissionModalViewModel BuildRolePermissionModal(
        NastaveniPanelViewModel panel,
        RolePermissionScopeViewModel? mapping,
        int? roleId,
        int? permissionId,
        int? userId,
        int? projektId);
    void ApplyPermissionCatalogDefaults(SaveAuthzPermissionCommand command, IReadOnlyList<PermissionCategoryViewModel> permissionCategories);
}
