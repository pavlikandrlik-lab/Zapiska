using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Common;

public interface ICommentAuthorizationPolicy
{
    bool CanCommentAsSubsystemLeader(CurrentUserContextViewModel currentUser, int projektId, int subsystemLeadOsobaId);
    bool CanAddComment(CurrentUserContextViewModel currentUser, int projektId, int subsystemLeadOsobaId);
    bool CanModifyComment(CurrentUserContextViewModel currentUser, int projektId, int subsystemLeadOsobaId, int commentAuthorOsobaId);
}
