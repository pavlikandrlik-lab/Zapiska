using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

public sealed class RecordsDataStore(IPmTrackerDataStore dataStore) : IRecordsDataStore
{
    public bool ProjektExists(int id) => dataStore.ProjektExists(id);

    public ProjektDetailViewModel BuildProjektDetail(int id) => dataStore.BuildProjektDetail(id);

    public ZaznamEditViewModel BuildZaznamEdit(int id) => dataStore.BuildZaznamEdit(id);

    public ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId = null) => dataStore.BuildZaznamCreate(projektId, jednaniId);

    public DeleteRecordModalViewModel BuildDeleteRecordModal(int projektId, int zaznamId) => dataStore.BuildDeleteRecordModal(projektId, zaznamId);

    public int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser) => dataStore.SaveRecord(command, currentUser);

    public void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser) => dataStore.DeleteRecord(command, currentUser);

    public void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser) => dataStore.AssignMeetingIdentifier(command, currentUser);

    public void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser) => dataStore.AddComment(command, currentUser);

    public void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser) => dataStore.UpdateComment(command, currentUser);

    public void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser) => dataStore.DeleteComment(command, currentUser);
}
