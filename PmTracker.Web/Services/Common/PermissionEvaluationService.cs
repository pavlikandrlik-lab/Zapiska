using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public sealed class PermissionEvaluationService : IPermissionEvaluationService
{
    public bool HasPermission(CurrentUserContextViewModel currentUser, string permissionKey, int? projektId = null)
    {
        return currentUser.HasPermission(permissionKey, projektId);
    }
}
