using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Dashboard;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Route("dashboard")]
public sealed class DashboardController : BaseController
{
    private const int FocusHomepageLimit = 8;
    private const int MeetingsHomepageLimit = 5;
    private const int NewsHomepageLimit = 5;
    private const int NewsListDefaultTake = 50;

    private readonly IDashboardService _dashboardService;

    public DashboardController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IDashboardService dashboardService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("~/")]
    [HttpGet("")]
    public IActionResult Index()
    {
        return View(AttachCurrentUser(_dashboardService.BuildDashboardPage(CurrentUserContext)));
    }

    [HttpGet("focus-panel")]
    public async Task<IActionResult> FocusPanel(CancellationToken ct = default)
    {
        var model = await _dashboardService.BuildFocusPanelAsync(CurrentUserContext, FocusHomepageLimit, ct);
        PrepareFocusPresentation(model.Items);
        return PartialView("~/Views/Dashboard/_DashboardFocusPanel.cshtml", model);
    }

    [HttpGet("meetings-panel")]
    public async Task<IActionResult> MeetingsPanel(CancellationToken ct = default)
    {
        var model = await _dashboardService.BuildMeetingsPanelAsync(CurrentUserContext, MeetingsHomepageLimit, ct);
        PrepareMeetingsPresentation(model.Items);
        return PartialView("~/Views/Dashboard/_DashboardMeetingsPanel.cshtml", model);
    }

    [HttpGet("news-panel")]
    public async Task<IActionResult> NewsPanel(int? take, CancellationToken ct = default)
    {
        var model = await _dashboardService.BuildNewsPanelAsync(CurrentUserContext, take ?? NewsHomepageLimit, ct);
        PrepareNewsPresentation(model.Items);
        return PartialView("~/Views/Dashboard/_DashboardNewsPanel.cshtml", model);
    }

    [HttpGet("focus")]
    public async Task<IActionResult> Focus(CancellationToken ct = default)
    {
        var model = AttachCurrentUser(await _dashboardService.BuildFocusListPageAsync(CurrentUserContext, ct));
        PrepareFocusPresentation(model.Items);
        return View(model);
    }

    [HttpGet("meetings")]
    public async Task<IActionResult> Meetings(CancellationToken ct = default)
    {
        var model = AttachCurrentUser(await _dashboardService.BuildMeetingsListPageAsync(CurrentUserContext, ct));
        PrepareMeetingsPresentation(model.Items);
        return View(model);
    }

    [HttpGet("news")]
    public async Task<IActionResult> News(int? take, CancellationToken ct = default)
    {
        var model = AttachCurrentUser(await _dashboardService.BuildNewsListPageAsync(CurrentUserContext, take ?? NewsListDefaultTake, ct));
        PrepareNewsPresentation(model.Items);
        return View(model);
    }

    private void PrepareFocusPresentation(IEnumerable<DashboardFocusItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.DetailUrl = Url.Action("Detail", "Projekty", new
            {
                id = item.ProjectId,
                tab = "zaznamy",
                recordId = item.RecordId
            }) ?? $"/Projekty/Detail/{item.ProjectId}?tab=zaznamy&recordId={item.RecordId}";
        }
    }

    private void PrepareMeetingsPresentation(IEnumerable<DashboardMeetingItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.DetailUrl = Url.Action("Detail", "Jednani", new { id = item.MeetingId, returnUrl = "/dashboard/meetings" })
                ?? $"/Jednani/Detail/{item.MeetingId}";
            item.PrintPdfUrl = Url.Action("JednaniTisk", "Export", new { jednaniId = item.MeetingId, autoPrint = true })
                ?? $"/Export/Jednani/{item.MeetingId}/Tisk?autoPrint=true";
            item.PrintWordUrl = Url.Action("JednaniWord", "Export", new { jednaniId = item.MeetingId })
                ?? $"/Export/Jednani/{item.MeetingId}/Word";
        }
    }

    private void PrepareNewsPresentation(IEnumerable<DashboardNewsItemViewModel> items)
    {
        foreach (var item in items)
        {
            if (string.Equals(item.EntityType, "jednani", StringComparison.OrdinalIgnoreCase) && item.MeetingId.HasValue)
            {
                item.DetailUrl = Url.Action("Detail", "Jednani", new { id = item.MeetingId.Value, returnUrl = "/dashboard/news" })
                    ?? $"/Jednani/Detail/{item.MeetingId.Value}";
                continue;
            }

            if (item.ProjectId.HasValue && item.RecordId.HasValue)
            {
                item.DetailUrl = Url.Action("Detail", "Projekty", new
                {
                    id = item.ProjectId.Value,
                    tab = "zaznamy",
                    recordId = item.RecordId.Value,
                    openComments = item.OpensComments
                }) ?? $"/Projekty/Detail/{item.ProjectId.Value}?tab=zaznamy&recordId={item.RecordId.Value}";
            }
        }
    }
}
