using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Web.Controllers;

[Authorize]
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

        const int MaxSteps = 500;  // reasonable upper bound for schedule preview; tune if business evidence suggests otherwise
        if (request.Steps.Count > MaxSteps)
        {
            return BadRequest(new { error = $"Maximální počet kroků je {MaxSteps}." });
        }

        var result = _previewService.Compute(request);
        return Json(result);
    }
}
