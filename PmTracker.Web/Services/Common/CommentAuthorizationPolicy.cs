using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public sealed class CommentAuthorizationPolicy : ICommentAuthorizationPolicy
{
    private readonly IPermissionEvaluationService _permissionEvaluationService;

    public CommentAuthorizationPolicy(IPermissionEvaluationService permissionEvaluationService)
    {
        _permissionEvaluationService = permissionEvaluationService;
    }

    public bool CanCommentAsSubsystemLeader(CurrentUserContextViewModel currentUser, int projektId, int subsystemLeadOsobaId)
    {
        if (subsystemLeadOsobaId <= 0 || currentUser.OsobaId <= 0)
        {
            return false;
        }

        if (currentUser.OsobaId != subsystemLeadOsobaId)
        {
            return false;
        }

        return _permissionEvaluationService.HasPermission(currentUser, PermissionKeys.RecordsCommentSubsystemLead, projektId);
    }

    public bool CanAddComment(CurrentUserContextViewModel currentUser, int projektId, int subsystemLeadOsobaId)
    {
        if (_permissionEvaluationService.HasPermission(currentUser, PermissionKeys.RecordsEdit, projektId))
        {
            return true;
        }

        return CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadOsobaId);
    }

    public bool CanModifyComment(CurrentUserContextViewModel currentUser, int projektId, int subsystemLeadOsobaId, int commentAuthorOsobaId)
    {
        if (_permissionEvaluationService.HasPermission(currentUser, PermissionKeys.RecordsEdit, projektId))
        {
            return true;
        }

        return commentAuthorOsobaId > 0
            && currentUser.OsobaId == commentAuthorOsobaId
            && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadOsobaId);
    }
}
