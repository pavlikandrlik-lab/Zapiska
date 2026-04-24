using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Policy = "permission:dashboard.view")]
    public async Task<IActionResult> Index(int id, string? dashTab = null, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = AttachCurrentUser(await _dashboardService.BuildDashboardPageAsync(id, ct));
        if (!string.IsNullOrWhiteSpace(dashTab))
        {
            model.ActiveTab = dashTab;
        }

        return View(model);
    }

    [HttpGet("records-panel")]
    [Authorize(Policy = "permission:dashboard.records.view")]
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
    [Authorize(Policy = "permission:dashboard.statistics.view")]
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
    [Authorize(Policy = "permission:dashboard.nes.view")]
    public async Task<IActionResult> NesPanel(int id, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(id, ct))
        {
            return Forbid();
        }

        var localNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime;
        var model = await _dashboardService.BuildNesPanelAsync(id, localNow, ct);
        return PartialView("~/Views/ProjectDashboard/_NesPanel.cshtml", model);
    }

    [HttpGet("vyzvy-panel")]
    [Authorize(Policy = "permission:dashboard.vyzvy.view")]
    public async Task<IActionResult> VyzvyPanel(int id, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(id, ct))
        {
            return Forbid();
        }

        // Per-action redesign 2026-04-23: muzeEditovat je nyní plynule HasPermission check
        // (žádné hardcoded role codes v service). Klíč vyzvy.create pokrývá oprávnění
        // spravovat výzvy projektu.
        var muzeEditovat = CurrentUserContext.HasPermission(PermissionKeys.VyzvyCreate, id);
        var model = await _dashboardService.BuildVyzvyPanelAsync(id, muzeEditovat, ct);
        return PartialView("~/Views/ProjectDashboard/_VyzvyPanel.cshtml", model);
    }

    private Task<bool> EnsureDashboardAccessAsync(int projectId, CancellationToken ct)
    {
        return Task.FromResult(CurrentUserContext.CanAccessProject(projectId));
    }
}
