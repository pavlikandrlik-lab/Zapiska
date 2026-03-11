using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

public sealed class RecordsService : IRecordsService
{
    private readonly IPmTrackerDataStore _dataStore;

    public RecordsService(IPmTrackerDataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public bool ProjektExists(int id) => _dataStore.ProjektExists(id);

    public ProjektDetailViewModel BuildProjektDetail(int id) => _dataStore.BuildProjektDetail(id);

    public ZaznamEditViewModel BuildZaznamEdit(int id) => _dataStore.BuildZaznamEdit(id);

    public ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId = null) => _dataStore.BuildZaznamCreate(projektId, jednaniId);

    public DeleteRecordModalViewModel BuildDeleteRecordModal(int projektId, int zaznamId) => _dataStore.BuildDeleteRecordModal(projektId, zaznamId);

    public int SaveRecord(SaveRecordCommand command, CurrentUserContextViewModel currentUser) => _dataStore.SaveRecord(command, currentUser);

    public void DeleteRecord(DeleteRecordCommand command, CurrentUserContextViewModel currentUser) => _dataStore.DeleteRecord(command, currentUser);

    public void AssignMeetingIdentifier(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser) => _dataStore.AssignMeetingIdentifier(command, currentUser);

    public void AddComment(AddCommentCommand command, CurrentUserContextViewModel currentUser) => _dataStore.AddComment(command, currentUser);

    public void UpdateComment(UpdateCommentCommand command, CurrentUserContextViewModel currentUser) => _dataStore.UpdateComment(command, currentUser);

    public void DeleteComment(DeleteCommentCommand command, CurrentUserContextViewModel currentUser) => _dataStore.DeleteComment(command, currentUser);
}
