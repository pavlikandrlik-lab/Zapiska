using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records.Commands;

public interface ISaveRecordCommandHandler
{
    int Handle(SaveRecordCommand command, CurrentUserContextViewModel currentUser);
}
