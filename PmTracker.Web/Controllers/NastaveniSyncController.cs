using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.Sync;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Controllers;

[Authorize(Policy = "permission:settings.view")]
[Route("Nastaveni/Sync")]
public sealed class NastaveniSyncController : BaseController
{
    private readonly IEnumerable<ISyncJobAdminHandler> _handlers;

    public NastaveniSyncController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IEnumerable<ISyncJobAdminHandler> handlers)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _handlers = handlers;
    }

    private ISyncJobAdminHandler? FindHandler(string jobKey)
        => _handlers.FirstOrDefault(h => string.Equals(h.JobKey, jobKey, StringComparison.OrdinalIgnoreCase));

    [HttpPost("{jobKey}/Save")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:settings.manage")]
    public async Task<IActionResult> Save(string jobKey, SyncJobSettingsInputModel input, CancellationToken ct)
    {
        var handler = FindHandler(jobKey);
        if (handler is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            TempData["SyncSaveError"] = "Neplatné hodnoty — zkontroluj periodu a anchor.";
            return RedirectToAction("Index", "Nastaveni", new { section = "synchronizace" });
        }

        // Ensure JobKey z route má přednost — input model by měl ladit.
        input.JobKey = handler.JobKey;

        await handler.SaveAsync(input, CurrentUserContext.OsobaId, ct);
        TempData["SyncSaveSuccess"] = $"Nastavení '{jobKey}' uloženo.";
        return RedirectToAction("Index", "Nastaveni", new { section = "synchronizace" });
    }

    [HttpPost("{jobKey}/RunNow")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:settings.manage")]
    public async Task<IActionResult> RunNow(string jobKey, CancellationToken ct)
    {
        var handler = FindHandler(jobKey);
        if (handler is null)
        {
            return NotFound();
        }

        var outcome = await handler.TriggerManualRunAsync(CurrentUserContext.OsobaId, ct);

        if (!outcome.Accepted)
        {
            TempData["SyncRunNowStatus"] = outcome.Message;
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            Response.Headers.RetryAfter = "60";
            return RedirectToAction("Index", "Nastaveni", new { section = "synchronizace" });
        }

        TempData["SyncRunNowStatus"] = outcome.Message;
        return RedirectToAction("Index", "Nastaveni", new { section = "synchronizace" });
    }
}
