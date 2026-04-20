using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("projekty/{id:int}/dashboard")]
public sealed class ProjectDashboardController : BaseController
{
    private readonly IProjectDashboardService _dashboardService;
    private readonly TimeProvider _timeProvider;

    public ProjectDashboardController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IProjectDashboardService dashboardService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _dashboardService = dashboardService;
        _timeProvider = timeProvider;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int id, string? dashTab = null, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        if (!CurrentUserContext.IsSuperAdmin
            && !await _dashboardService.CanAccessDashboardAsync(id, CurrentUserContext.OsobaId, ct))
        {
            return Forbid();
        }

        var model = AttachCurrentUser(await _dashboardService.BuildDashboardPageAsync(id, ct));
        if (!string.IsNullOrWhiteSpace(dashTab))
        {
            model.ActiveTab = dashTab;
        }

        return View(model);
    }

    [HttpGet("records-panel")]
    public async Task<IActionResult> RecordsPanel(int id, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(id, ct))
        {
            return Forbid();
        }

        var localNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime;
        var model = await _dashboardService.BuildRecordsPanelAsync(id, localNow, ct);
        return PartialView("~/Views/ProjectDashboard/_RecordsPanel.cshtml", model);
    }

    [HttpGet("statistics-panel")]
    public async Task<IActionResult> StatisticsPanel(int id, int? year = null, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(id, ct))
        {
            return Forbid();
        }

        var currentYear = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).Year;
        var model = await _dashboardService.BuildStatisticsPanelAsync(id, year ?? currentYear, ct);
        return PartialView("~/Views/ProjectDashboard/_StatisticsPanel.cshtml", model);
    }

    [HttpGet("nes-panel")]
    public async Task<IActionResult> NesPanel(int id, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(id, ct))
        {
            return Forbid();
        }

        var model = _dashboardService.BuildNesPanel();
        return PartialView("~/Views/ProjectDashboard/_NesPanel.cshtml", model);
    }

    [HttpGet("vyzvy-panel")]
    public async Task<IActionResult> VyzvyPanel(int id, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(id, ct))
        {
            return Forbid();
        }

        var model = await _dashboardService.BuildVyzvyPanelAsync(
            id,
            CurrentUserContext.OsobaId,
            CurrentUserContext.IsSuperAdmin,
            ct);
        return PartialView("~/Views/ProjectDashboard/_VyzvyPanel.cshtml", model);
    }

    private async Task<bool> EnsureDashboardAccessAsync(int projectId, CancellationToken ct)
    {
        if (!CurrentUserContext.CanAccessProject(projectId))
        {
            return false;
        }

        return CurrentUserContext.IsSuperAdmin
            || await _dashboardService.CanAccessDashboardAsync(projectId, CurrentUserContext.OsobaId, ct);
    }
}
