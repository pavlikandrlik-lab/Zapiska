using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Controllers;

public sealed partial class JednaniController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SaveMeetingCommand command, CancellationToken ct = default)
    {
        EnsureReadableMeetingTimeError();

        if (command.Id.HasValue)
        {
            if (!CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId))
            {
                return Forbid();
            }

            var isEditable = await _meetingService.IsMeetingEditableAsync(command.ProjektId, command.Id.Value, ct);
            if (!isEditable.HasValue)
            {
                return NotFound();
            }

            if (!isEditable.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        return await ExecuteValidatedCommandAsync(
            hasPermission: () => command.Id.HasValue
                ? CurrentUserContext.HasPermission(PermissionKeys.MeetingsEdit, command.ProjektId)
                : CurrentUserContext.HasPermission(PermissionKeys.MeetingsCreate, command.ProjektId),
            invalidAjaxMessage: "Poradu nelze uložit.",
            invalidFallbackMessage: InvalidFormFallbackMessage,
            onInvalidRedirect: () => RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "jednani" }),
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "jednani" })!),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "projekty-detail-jednani",
                refreshUrl: Url.Action("JednaniTabPartial", "Projekty", new { id = command.ProjektId }),
                projectId: command.ProjektId,
                tab: "jednani",
                message: "Jednání bylo uloženo.")),
            operation: () => _meetingService.SaveMeetingAsync(command, CurrentUserContext, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:meetings.delete")]
    public Task<IActionResult> Delete(DeleteMeetingCommand command, string? returnUrl, CancellationToken ct = default)
    {
        IActionResult RedirectAfterDelete()
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Detail", "Projekty", new { id = command.ProjektId, tab = "jednani" });
        }

        return ExecuteCommandAsync(
            hasPermission: () => true,
            onSuccessRedirect: () => Task.FromResult<IActionResult>(RedirectAfterDelete()),
            onAjaxSuccess: () => Task.FromResult<IActionResult>(AjaxSuccessResult(
                refreshScope: "projekty-detail-jednani",
                refreshUrl: Url.Action("JednaniTabPartial", "Projekty", new { id = command.ProjektId }),
                projectId: command.ProjektId,
                tab: "jednani",
                message: "Jednání bylo smazáno.")),
            operation: () => _meetingService.DeleteMeetingAsync(command, CurrentUserContext, ct));
    }

    private void EnsureReadableMeetingTimeError()
    {
        var keys = new[] { nameof(SaveMeetingCommand.CasZacatek), $"command.{nameof(SaveMeetingCommand.CasZacatek)}" };
        var hasCasError = keys.Any(key => ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0);
        if (!hasCasError)
        {
            return;
        }

        var hasReadableError = keys
            .Where(key => ModelState.TryGetValue(key, out _))
            .SelectMany(key => ModelState[key]!.Errors)
            .Any(error => !string.IsNullOrWhiteSpace(error.ErrorMessage)
                          && error.ErrorMessage.Contains("HH:mm", StringComparison.OrdinalIgnoreCase));

        if (!hasReadableError)
        {
            ModelState.AddModelError(nameof(SaveMeetingCommand.CasZacatek), "Vyberte čas začátku ve formátu HH:mm.");
        }
    }
}
