using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public interface ICommentAuthorizationPolicy
{
    bool CanCommentAsSubsystemLeader(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds);
    bool CanAddComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, bool isDraftMeeting);
    bool CanModifyComment(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, int commentAuthorOsobaId, bool isDraftMeeting);
    bool CanDeleteComment(CurrentUserContextViewModel currentUser, int projektId, int commentAuthorOsobaId);
    bool CanSaveMeetingNote(CurrentUserContextViewModel currentUser, int projektId, IReadOnlyCollection<int> subsystemLeadEquivalentOsobaIds, bool isDraftMeeting);
}
