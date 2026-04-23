using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

[Authorize]
public sealed class ScheduleController : BaseController
{
    private readonly SchedulePreviewService _previewService;

    public ScheduleController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        SchedulePreviewService previewService)
        : base(userContextResolver, timeProvider, loggerFactory)
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

        const int MaxSteps = 500;  // DoS guard — reasonable upper bound for schedule preview
        if (request.Steps.Count > MaxSteps)
        {
            return BadRequest(new { error = $"Maximální počet kroků je {MaxSteps}." });
        }

        // Per-action redesign 2026-04-23: schedule.preview policy (project-scoped).
        // ProjektId je vyžadovaný v body payloadu — client musí poslat při volání
        // /Schedule/Recalc. JavaScript vrstva (block.js fetchSchedulePreview) ho
        // připraví ve F5.
        if (request.ProjektId <= 0)
        {
            return BadRequest(new { error = "Chybí ProjektId pro autorizaci preview." });
        }
        if (!CurrentUserContext.HasPermission(PermissionKeys.SchedulePreview, request.ProjektId))
        {
            return Forbid();
        }

        var result = _previewService.Compute(request);
        return Json(result);
    }
}
