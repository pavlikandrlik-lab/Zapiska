using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using System.Globalization;

namespace PmTracker.Web.Controllers;

public sealed partial class ProjektyController
{
    [HttpGet]
    public async Task<IActionResult> RecordsTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectRecordsTabAsync(id, ct);
        await PrepareProjectRecordsTabPresentationAsync(model, ct);
        return PartialView("~/Views/Projekty/_ProjectRecordsTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> RecordMeetingCommentStates(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var statesByRecordId = await _projectService.BuildRecordMeetingCommentStatesAsync(id, ct);
        return Json(new ProjektMeetingCommentStatesResponseViewModel
        {
            StatesByRecordId = statesByRecordId.ToDictionary(
                item => item.Key.ToString(CultureInfo.InvariantCulture),
                item => item.Value)
        });
    }

    [HttpGet]
    public async Task<IActionResult> SearchProjectMemberCandidates(int id, [FromQuery(Name = "q")] string? query, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        if (!CurrentUserContext.HasPermission(PermissionKeys.TeamManage, id))
        {
            return Forbid();
        }

        var results = await _projectService.SearchProjectMemberCandidatesAsync(query ?? string.Empty, ct);
        return Json(new PersonPickerSearchResponseViewModel
        {
            Results = results
        });
    }

    [HttpGet]
    public async Task<IActionResult> HarmonogramTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectScheduleTabAsync(id, ct);
        PrepareProjectScheduleTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectScheduleTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> JednaniTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectMeetingsTabAsync(id, ct);
        PrepareProjectMeetingsTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectMeetingsTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> TymTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        var model = await _projectService.BuildProjectTeamTabAsync(id, ct);
        PrepareProjectTeamTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectTeamTab.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> NavrhyTabPartial(int id, CancellationToken ct = default)
    {
        if (!CurrentUserContext.CanAccessProject(id))
        {
            return NotFound();
        }

        if (!await _recordProposalService.CanViewProposalTabAsync(id, CurrentUserContext, ct))
        {
            return Forbid();
        }

        var model = await _recordProposalService.BuildProjectProposalsTabAsync(id, CurrentUserContext, ct);
        PrepareProjectProposalsTabPresentation(model);
        return PartialView("~/Views/Projekty/_ProjectProposalsTab.cshtml", model);
    }
}
