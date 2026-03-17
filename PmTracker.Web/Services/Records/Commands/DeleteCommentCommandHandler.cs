using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public sealed class DeleteCommentCommandHandler(IRecordsDataStore dataStore) : IDeleteCommentCommandHandler
{
    public void Handle(DeleteCommentCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.DeleteComment(command, currentUser);
}
