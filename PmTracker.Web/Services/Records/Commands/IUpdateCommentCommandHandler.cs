using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public interface IUpdateCommentCommandHandler
{
    void Handle(UpdateCommentCommand command, CurrentUserContextViewModel currentUser);
}
