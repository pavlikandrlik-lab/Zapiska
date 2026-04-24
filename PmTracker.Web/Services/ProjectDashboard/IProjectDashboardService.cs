using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public interface IProjectDashboardService
{
    Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default);
    Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default);
    Task<ProjectDashboardStatisticsPanelViewModel> BuildStatisticsPanelAsync(int projectId, int year, CancellationToken ct = default);

    /// <summary>
    /// Panel "NES v prodlení" — pokud je projekt napojen na IS (<c>projekty.servicedesk_info_system_id</c>),
    /// dodá seznam ticketů v prodlení přes <see cref="PmTracker.ServiceDesk.Contracts.IInformacniSystemQueryService"/>.
    /// Plán 5 Sprint B Task 4.
    /// </summary>
    Task<ProjectDashboardNesPanelViewModel> BuildNesPanelAsync(int projektId, DateTime reference, CancellationToken ct = default);

    Task<ProjectDashboardVyzvyPanelViewModel> BuildVyzvyPanelAsync(int projektId, bool muzeEditovat, CancellationToken ct);
    // CanAccessDashboardAsync smazáno v redesignu 2026-04-23 — dashboard.view policy atribut na endpointech.
}
