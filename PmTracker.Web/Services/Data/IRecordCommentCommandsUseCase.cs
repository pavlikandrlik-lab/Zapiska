using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IRecordCommentCommandsUseCase
{
    void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser);
    void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser);
    void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser);
}
