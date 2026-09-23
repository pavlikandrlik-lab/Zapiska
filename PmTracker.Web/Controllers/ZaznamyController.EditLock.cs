using Microsoft.AspNetCore.Mvc;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Spec 2026-09-17 §4.2 — uvolnění zámku karty při odchodu z editoru.
/// </summary>
public sealed partial class ZaznamyController
{
    public sealed record ReleaseEditLockRequest(int ZaznamId);

    /// <summary>
    /// Volá se beaconem z <c>pagehide</c> (recordEditor/editLock.js). Antiforgery token
    /// se tu nevyžaduje — sendBeacon ho nepřipojí a operace maže výhradně VLASTNÍ zámek
    /// volajícího, takže podvržený požadavek nikomu nic neodemkne. Aplikace antiforgery
    /// nevaliduje globálně, jen u akcí s [ValidateAntiForgeryToken].
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReleaseEditLock(
        [FromBody] ReleaseEditLockRequest request,
        CancellationToken ct = default)
    {
        if (request is null || request.ZaznamId <= 0)
        {
            return BadRequest();
        }

        await _editLockService.ReleaseAsync(request.ZaznamId, CurrentUserContext.OsobaId, ct);
        return Ok();
    }
}
