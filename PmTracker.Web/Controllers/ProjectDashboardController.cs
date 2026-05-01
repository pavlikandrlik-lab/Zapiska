using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Export;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

// 2026-04-30 fix: route param je `projektId`, ne `id`. PermissionAuthorizationHandler
// extrahuje projektId přes `route.TryGetValue("projektId", ...)` — pokud klíč chybí,
// fallback na global-scope check selže pro běžné users (dashboard.view je project-scoped
// permission). Sjednoceno s konvencí ostatních controllerů (např. ExportController).
[Route("projekty/{projektId:int}/dashboard")]
public sealed class ProjectDashboardController : BaseController
{
    private readonly IProjectDashboardService _dashboardService;
    private readonly INesPanelExcelExportService _nesExcelExport;
    private readonly TimeProvider _timeProvider;

    public ProjectDashboardController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IProjectDashboardService dashboardService,
        INesPanelExcelExportService nesExcelExport)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _dashboardService = dashboardService;
        _nesExcelExport = nesExcelExport;
        _timeProvider = timeProvider;
    }

    [HttpGet("")]
    [Authorize(Policy = "permission:dashboard.view")]
    public async Task<IActionResult> Index(int projektId, string? dashTab = null, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(projektId))
        {
            return NotFound();
        }

        var model = AttachCurrentUser(await _dashboardService.BuildDashboardPageAsync(projektId, ct));
        if (!string.IsNullOrWhiteSpace(dashTab))
        {
            model.ActiveTab = dashTab;
        }

        return View(model);
    }

    [HttpGet("records-panel")]
    [Authorize(Policy = "permission:dashboard.records.view")]
    public async Task<IActionResult> RecordsPanel(int projektId, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(projektId, ct))
        {
            return Forbid();
        }

        var localNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime;
        var model = await _dashboardService.BuildRecordsPanelAsync(projektId, localNow, ct);
        return PartialView("~/Views/ProjectDashboard/_RecordsPanel.cshtml", model);
    }

    [HttpGet("statistics-panel")]
    [Authorize(Policy = "permission:dashboard.statistics.view")]
    public async Task<IActionResult> StatisticsPanel(int projektId, int? year = null, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(projektId, ct))
        {
            return Forbid();
        }

        var currentYear = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).Year;
        var model = await _dashboardService.BuildStatisticsPanelAsync(projektId, year ?? currentYear, ct);
        return PartialView("~/Views/ProjectDashboard/_StatisticsPanel.cshtml", model);
    }

    [HttpGet("nes-panel")]
    [Authorize(Policy = "permission:dashboard.nes.view")]
    public async Task<IActionResult> NesPanel(int projektId, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(projektId, ct))
        {
            return Forbid();
        }

        var localNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime;
        var model = await _dashboardService.BuildNesPanelAsync(projektId, localNow, ct);
        return PartialView("~/Views/ProjectDashboard/_NesPanel.cshtml", model);
    }

    /// <summary>
    /// Plán 4 Feature C gap #4 (2026-04-24) — Excel export NES panelu.
    /// Stejný permission gate jako NesPanel. Filename: NES-projekt-{projektId}-{yyyyMMdd}.xlsx.
    /// </summary>
    [HttpGet("nes-panel/export")]
    [Authorize(Policy = "permission:dashboard.nes.view")]
    public async Task<IActionResult> NesPanelExport(int projektId, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(projektId, ct))
        {
            return Forbid();
        }

        var localNow = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), TimeZoneInfo.Local).DateTime;
        var model = await _dashboardService.BuildNesPanelAsync(projektId, localNow, ct);
        if (!model.IsServiceDeskIntegrated)
        {
            return NotFound(new { error = "Projekt nemá napojení na ServiceDesk, export není k dispozici." });
        }

        var bytes = _nesExcelExport.Build(model, localNow);
        var filename = $"NES-projekt-{projektId}-{localNow:yyyyMMdd}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    [HttpGet("vyzvy-panel")]
    [Authorize(Policy = "permission:dashboard.vyzvy.view")]
    public async Task<IActionResult> VyzvyPanel(int projektId, CancellationToken ct = default)
    {
        if (!await EnsureDashboardAccessAsync(projektId, ct))
        {
            return Forbid();
        }

        // Per-action redesign 2026-04-23: muzeEditovat je nyní plynule HasPermission check
        // (žádné hardcoded role codes v service). Klíč vyzvy.create pokrývá oprávnění
        // spravovat výzvy projektu.
        var muzeEditovat = CurrentUserContext.HasPermission(PermissionKeys.VyzvyCreate, projektId);
        var model = await _dashboardService.BuildVyzvyPanelAsync(projektId, muzeEditovat, ct);
        return PartialView("~/Views/ProjectDashboard/_VyzvyPanel.cshtml", model);
    }

    private Task<bool> EnsureDashboardAccessAsync(int projectId, CancellationToken ct)
    {
        return Task.FromResult(CurrentUserContext.CanAccessProject(projectId));
    }
}
