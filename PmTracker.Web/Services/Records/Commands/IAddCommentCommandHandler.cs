using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public interface IAddCommentCommandHandler
{
    void Handle(AddCommentCommand command, CurrentUserContextViewModel currentUser);
}
