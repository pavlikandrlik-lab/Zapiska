using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public sealed class UpdateCommentCommandHandler(IRecordsDataStore dataStore) : IUpdateCommentCommandHandler
{
    public void Handle(UpdateCommentCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.UpdateComment(command, currentUser);
}
