using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

public interface IRecordsService
{
    bool ProjektExists(int id);
    ProjektDetailViewModel BuildProjektDetail(int id);
    ZaznamEditViewModel BuildZaznamEdit(int id);
    ZaznamEditViewModel BuildZaznamCreate(int projektId);
    int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser);
    void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser);
    void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser);
    void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser);
    void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser);
}
