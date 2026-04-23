using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public interface IProjectDashboardService
{
    Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default);
    Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default);
    Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default);
    ProjectDashboardNesPanelViewModel BuildNesPanel();
    Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(int projektId, bool muzeEditovat, CancellationToken ct);
    // CanAccessDashboardAsync smazáno v redesignu 2026-04-23 — dashboard.view policy atribut na endpointech.
}
