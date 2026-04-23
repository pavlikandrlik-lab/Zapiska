using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Controllers;

public sealed partial class JednaniController
{
    [HttpGet]
    [Authorize(Policy = "permission:meetings.create")]
    public async Task<IActionResult> NewMeetingModal(int projektId, CancellationToken ct = default)
    {
        if (!await _projectService.ProjektExistsAsync(projektId, ct))
        {
            return NotFound();
        }

        var model = await _meetingService.BuildNewMeetingModalAsync(projektId, GetLocalNow(), ct);
        return View("NewMeetingModal", model);
    }

    [HttpGet]
    [Authorize(Policy = "permission:meetings.edit")]
    public async Task<IActionResult> EditMeetingModal(int projektId, int meetingId, CancellationToken ct = default)
    {
        var isEditable = await _meetingService.IsMeetingEditableAsync(projektId, meetingId, ct);
        if (!isEditable.HasValue)
        {
            return NotFound();
        }

        if (!isEditable.Value)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var model = await _meetingService.BuildEditMeetingModalAsync(projektId, meetingId, ct);
        if (model is null)
        {
            return NotFound();
        }

        return View("NewMeetingModal", model);
    }
}
