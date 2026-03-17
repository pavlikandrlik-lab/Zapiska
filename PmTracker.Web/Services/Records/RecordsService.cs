using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records.Commands;
using PmTracker.Web.Services.Records.Queries;

namespace PmTracker.Web.Services.Records;

public sealed class RecordsService(
    IProjektExistsQueryHandler projektExistsQueryHandler,
    IBuildProjektDetailQueryHandler buildProjektDetailQueryHandler,
    IBuildZaznamEditQueryHandler buildZaznamEditQueryHandler,
    IBuildZaznamCreateQueryHandler buildZaznamCreateQueryHandler,
    IBuildDeleteRecordModalQueryHandler buildDeleteRecordModalQueryHandler,
    ISaveRecordCommandHandler saveRecordCommandHandler,
    IDeleteRecordCommandHandler deleteRecordCommandHandler,
    IAssignMeetingIdentifierCommandHandler assignMeetingIdentifierCommandHandler,
    IAddCommentCommandHandler addCommentCommandHandler,
    IUpdateCommentCommandHandler updateCommentCommandHandler,
    IDeleteCommentCommandHandler deleteCommentCommandHandler) : IRecordsService
{
    public bool ProjektExists(int id) => projektExistsQueryHandler.Handle(id);

    public ProjektDetailViewModel BuildProjektDetail(int id) => buildProjektDetailQueryHandler.Handle(id);

    public ZaznamEditViewModel BuildZaznamEdit(int id) => buildZaznamEditQueryHandler.Handle(id);

    public ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId = null) => buildZaznamCreateQueryHandler.Handle(projektId, jednaniId);

    public DeleteRecordModalViewModel BuildDeleteRecordModal(int projektId, int zaznamId) => buildDeleteRecordModalQueryHandler.Handle(projektId, zaznamId);

    public int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser) => saveRecordCommandHandler.Handle(command, currentUser);

    public void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser) => deleteRecordCommandHandler.Handle(command, currentUser);

    public void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser) => assignMeetingIdentifierCommandHandler.Handle(command, currentUser);

    public void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser) => addCommentCommandHandler.Handle(command, currentUser);

    public void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser) => updateCommentCommandHandler.Handle(command, currentUser);

    public void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser) => deleteCommentCommandHandler.Handle(command, currentUser);
}
