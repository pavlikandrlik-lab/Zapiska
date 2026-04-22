using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public sealed class CommentAuthorizationPolicy : ICommentAuthorizationPolicy
{
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

        return currentUser.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projektId);
    }

    public bool CanAddComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, bool isDraftMeeting)
    {
        if (currentUser.HasPermission(PermissionKeys.RecordsEdit, projektId))
        {
            return true;
        }

        return isDraftMeeting && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
    }

    public bool CanModifyComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, int commentAuthorOsobaId, bool isDraftMeeting)
    {
        if (currentUser.HasPermission(PermissionKeys.RecordsEdit, projektId))
        {
            return true;
        }

        return commentAuthorOsobaId > 0
            && currentUser.OsobaId == commentAuthorOsobaId
            && isDraftMeeting
            && CanCommentAsSubsystemLeader(currentUser, projektId, subsystemLeadEquivalentOsobaIds);
    }
}
