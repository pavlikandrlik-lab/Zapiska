using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public interface ICommentAuthorizationPolicy
{
    bool CanCommentAsSubsystemLeader(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds);
    bool CanAddComment(CurrentUserContextViewModel currentUser, CommentScope scope);
    bool CanEditComment(CurrentUserContextViewModel currentUser, CommentScope scope, int commentAuthorOsobaId);
    bool CanDeleteComment(CurrentUserContextViewModel currentUser, CommentScope scope, int commentAuthorOsobaId);
    bool CanSaveMeetingNote(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, bool isDraftMeeting);
}
