using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IProjectDetailComposition
{
    IReadOnlyList<ProjectSubsystemViewModel> BuildActiveProjectSubsystems(int projectId);
    IReadOnlyList<ZaznamCardViewModel> BuildRecordCardsForProject(int projectId);
    IReadOnlyList<ProjektHarmonogramUkolViewModel> BuildProjectScheduleRows(IReadOnlyList<ZaznamCardViewModel> records);
    IReadOnlyList<JednaniListItemViewModel> BuildJednaniList(int projectId);
    IReadOnlyList<ProjectRoleGridRowViewModel> BuildUnifiedActiveProjectRoleRows(int projectId);
    IReadOnlyList<ProjectRoleHistoryGridRowViewModel> BuildUnifiedProjectRoleHistoryRows(int projectId);
    IReadOnlyList<ProjectMemberCandidateViewModel> BuildProjectMemberCandidates();
    IReadOnlyList<ProjectSubsystemOptionViewModel> BuildProjectSubsystemOptions(int projectId);
    IReadOnlyList<JednaniOptionViewModel> BuildOpenMeetingOptions(IReadOnlyList<JednaniListItemViewModel> meetings);
}
