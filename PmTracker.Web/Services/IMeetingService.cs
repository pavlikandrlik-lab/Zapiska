using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services;

public interface IMeetingService
{
    Task<IReadOnlyList<JednaniProjektListItemViewModel>> BuildJednaniOverviewAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projektId, CancellationToken ct = default);

    Task<JednaniDetailViewModel> BuildJednaniDetailAsync(int id, CancellationToken ct = default);
    Task<int?> GetMeetingProjectIdAsync(int meetingId, CancellationToken ct = default);
    Task<JednaniUkolViewModel?> GetSingleTaskAsync(int meetingId, int zaznamId, CancellationToken ct = default);
    Task<IReadOnlyList<MeetingParticipantCandidateViewModel>> BuildMeetingParticipantCandidatesAsync(int projectId, int meetingId, CancellationToken ct = default);

    Task<int> SaveMeetingAsync(SaveMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task DeleteMeetingAsync(DeleteMeetingCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveMeetingStatusAsync(SaveMeetingStatusCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task AddMeetingParticipantAsync(AddMeetingParticipantCommand command, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveAttendanceBatchAsync(int meetingId, IEnumerable<(int OsobaId, string StavUcasti)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
    Task SaveMeetingNotesBatchAsync(int meetingId, IEnumerable<(int ZaznamId, string Text)> rows, CurrentUserContextViewModel currentUser, CancellationToken ct = default);
}
