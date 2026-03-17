using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public interface IDeleteRecordCommandHandler
{
    void Handle(DeleteRecordCommand command, CurrentUserContextViewModel currentUser);
}
