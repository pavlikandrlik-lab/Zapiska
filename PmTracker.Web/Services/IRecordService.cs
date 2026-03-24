using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public interface IRecordService
{
    Task<bool> ProjektExistsAsync(int id, CancellationToken ct = default);
    Task<ProjektDetailViewModel> BuildProjektDetailAsync(int id, CancellationToken ct = default);
    Task<ZaznamEditViewModel> BuildZaznamEditAsync(int id, CancellationToken ct = default);
    Task<ZaznamEditViewModel> BuildZaznamCreateAsync(int projektId, int? jednaniId = null, CancellationToken ct = default);
    Task<DeleteRecordModalViewModel> BuildDeleteRecordModalAsync(int projektId, int zaznamId, CancellationToken ct = default);
    Task<int> SaveRecordAsync(SaveRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeleteRecordAsync(DeleteRecordCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task AssignMeetingIdentifierAsync(AssignMeetingIdentifierCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task AddCommentAsync(AddCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task UpdateCommentAsync(UpdateCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeleteCommentAsync(DeleteCommentCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
