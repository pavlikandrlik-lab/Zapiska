using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IRecordWriteCommandsUseCase
{
    int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser, IRecordWriteCommandsComposition composition);

    void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser);

    void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser, IRecordWriteCommandsComposition composition);
}
