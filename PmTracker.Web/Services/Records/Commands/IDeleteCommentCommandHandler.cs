using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public interface IDeleteCommentCommandHandler
{
    void Handle(DeleteCommentCommand command, CurrentUserContextViewModel currentUser);
}
