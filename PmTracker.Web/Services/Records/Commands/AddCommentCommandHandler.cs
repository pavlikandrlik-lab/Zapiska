using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public sealed class AddCommentCommandHandler(IRecordsDataStore dataStore) : IAddCommentCommandHandler
{
    public void Handle(AddCommentCommand command, CurrentUserContextViewModel currentUser)
        => dataStore.AddComment(command, currentUser);
}
