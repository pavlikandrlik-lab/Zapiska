using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.ProjectDashboard;

public interface IProjectDashboardService
{
    Task<ProjectDashboardPageViewModel> BuildDashboardPageAsync(int projectId, CancellationToken ct = default);
    Task<ProjectDashboardRecordsPanelViewModel> BuildRecordsPanelAsync(int projectId, DateTime referenceDate, CancellationToken ct = default);

    /// <summary>
    /// Záložka „NES v prodlení" nad penalizačním algoritmem (<see cref="ServiceDesk.Prodleni.IProdleniService"/>).
    /// Statistiky skládá <see cref="Zakladni.ZakladniReportBuilder"/>.
    /// </summary>
    Task<ProjectDashboardNesPanelViewModel> BuildNesPanelAsync(int projektId, DateTime reference, CancellationToken ct = default);

    // CanAccessDashboardAsync smazáno v redesignu 2026-04-23 — dashboard.view policy atribut na endpointech.
}
