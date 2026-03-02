using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public interface IPermissionEvaluationService
{
    bool HasPermission(CurrentUserContextViewModel currentUser, string permissionKey, int? projektId = null);
}
