using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IProjectDetailComposition
{
    Task<IReadOnlyList<ProjectSubsystemViewModel>> BuildActiveProjectSubsystemsAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ZaznamCardViewModel>> BuildRecordCardsForProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjektHarmonogramUkolViewModel>> BuildProjectScheduleRowsAsync(IReadOnlyList<ZaznamCardViewModel> records, CancellationToken ct = default);
    Task<IReadOnlyList<JednaniListItemViewModel>> BuildJednaniListAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectRoleGridRowViewModel>> BuildUnifiedActiveProjectRoleRowsAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectRoleHistoryGridRowViewModel>> BuildUnifiedProjectRoleHistoryRowsAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectMemberCandidateViewModel>> BuildProjectMemberCandidatesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProjectSubsystemOptionViewModel>> BuildProjectSubsystemOptionsAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<JednaniOptionViewModel>> BuildOpenMeetingOptionsAsync(IReadOnlyList<JednaniListItemViewModel> meetings, CancellationToken ct = default);
}
