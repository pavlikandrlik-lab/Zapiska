using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Controllers;

public sealed class ScheduleController : Controller
{
    private readonly SchedulePreviewService _previewService;

    public ScheduleController(SchedulePreviewService previewService)
    {
        _previewService = previewService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Recalc([FromBody] SchedulePreviewRequest request)
    {
        if (request is null || request.Steps.Count == 0)
        {
            return BadRequest();
        }
        var result = _previewService.Compute(request);
        return Json(result);
    }
}
