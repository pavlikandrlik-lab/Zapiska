using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public sealed class CommentAuthorizationPolicy : ICommentAuthorizationPolicy
{
    private readonly IPermissionEvaluationService _permissionEvaluationService;

    public CommentAuthorizationPolicy(IPermissionEvaluationService permissionEvaluationService)
    {
        _permissionEvaluationService = permissionEvaluationService;
    }

    public bool CanCommentAsSubsystemLeader(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds)
    {
        if (currentUser.OsobaId <= 0 || subsystemLeadEquivalentOsobaIds.Count == 0)
        {
            return false;
        }

        if (!subsystemLeadEquivalentOsobaIds.Contains(currentUser.OsobaId))
        {
            return false;
        }

        return _permissionEvaluationService.HasPermission(currentUser, PermissionKeys.RecordsCommentSubsystemLead, projektId);
    }

    public bool CanAddComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, bool isDraftMeeting)
    {
        if (_permissionEvaluationService.HasPermission(currentUser, PermissionKeys.RecordsEdit, projektId))
        {
            return true;
        }

        return isDraftMeeting && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
    }

    public bool CanModifyComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, int commentAuthorOsobaId, bool isDraftMeeting)
    {
        if (_permissionEvaluationService.HasPermission(currentUser, PermissionKeys.RecordsEdit, projektId))
        {
            return true;
        }

        return commentAuthorOsobaId > 0
            && currentUser.OsobaId == commentAuthorOsobaId
            && isDraftMeeting
            && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
    }
}
