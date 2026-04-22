using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.SDConnector;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Plán C Task 19 — diagnostická stránka /SDConnector.
/// Admin view stavu harvestu: počet externích odkazů, kolik je právě vytěženo,
/// per-řádek odkaz na re-harvest (přes existující endpoint <c>POST /Vyjadreni/ReHarvest</c>).
/// </summary>
[Authorize(Policy = "permission:settings.manage")]
[Route("SDConnector")]
public sealed class SDConnectorController : Controller
{
    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniHarvestService _harvest;
    private readonly TimeProvider _time;
    private readonly IOptions<TicketingOptions> _ticketingOptions;

    public SDConnectorController(
        PmTrackerDbContext db,
        IVyjadreniHarvestService harvest,
        TimeProvider time,
        IOptions<TicketingOptions> ticketingOptions)
    {
        _db = db;
        _harvest = harvest;
        _time = time;
        _ticketingOptions = ticketingOptions;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var options = _ticketingOptions.Value;
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var lastDay = nowUtc.AddDays(-1);

        var vm = new SDConnectorViewModel
        {
            TicketingEnabled = options.Enabled,
            TicketingConnectionName = options.ConnectionStringName
        };

        var eoRaw = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .Where(x => x.Cislo != null && x.Cislo != "")
            .Select(x => new { x.Id, x.ZaznamId, x.Cislo, x.LastHarvestedAt })
            .ToListAsync(ct).ConfigureAwait(false);
        var zaznamIds = eoRaw.Select(x => x.ZaznamId).Distinct().ToArray();
        var projektByZaznam = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => zaznamIds.Contains(x.Id))
            .Select(x => new { x.Id, x.ProjektId })
            .ToDictionaryAsync(x => x.Id, x => x.ProjektId, ct).ConfigureAwait(false);

        var bindings = await _db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Stav == (byte)VazbaStav.Active)
            .GroupBy(x => x.ExterniOdkazId)
            .Select(g => new
            {
                ExterniOdkazId = g.Key,
                Total = g.Count(),
                Auto = g.Count(v => v.Source == (byte)VazbaSource.Auto),
                Manual = g.Count(v => v.Source == (byte)VazbaSource.Manual),
            })
            .ToDictionaryAsync(x => x.ExterniOdkazId, ct).ConfigureAwait(false);

        var rows = eoRaw.Select(eo =>
        {
            bindings.TryGetValue(eo.Id, out var b);
            projektByZaznam.TryGetValue(eo.ZaznamId, out var projektId);
            return new SDConnectorExterniOdkazRow
            {
                ExterniOdkazId = eo.Id,
                ZaznamId = eo.ZaznamId,
                ProjektId = projektId,
                Cislo = eo.Cislo!,
                LastHarvestedAt = eo.LastHarvestedAt,
                ActiveBindingsCount = b?.Total ?? 0,
                AutoBindingsCount = b?.Auto ?? 0,
                ManualBindingsCount = b?.Manual ?? 0
            };
        })
        .OrderByDescending(x => x.LastHarvestedAt ?? DateTime.MinValue)
        .ToList();

        vm.ExterniOdkazCount = rows.Count;
        vm.HarvestedInLastDayCount = rows.Count(r => r.LastHarvestedAt.HasValue && r.LastHarvestedAt.Value >= lastDay);
        vm.NeverHarvestedCount = rows.Count(r => !r.LastHarvestedAt.HasValue);
        vm.Rows = rows;

        return View(vm);
    }

    [HttpPost("ReHarvest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReHarvest([FromForm] int externiOdkazId, CancellationToken ct)
    {
        if (externiOdkazId <= 0)
        {
            TempData["SDConnectorError"] = "Neplatný externiOdkazId.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _harvest.ReHarvestTicketAsync(externiOdkazId, ct).ConfigureAwait(false);
            TempData["SDConnectorMessage"] =
                $"Re-harvest id={externiOdkazId}: načteno {result.Fetched}, vytvořeno {result.Created}, superseded {result.Superseded}, preskočeno {result.Skipped}.";
        }
        catch (Exception ex)
        {
            TempData["SDConnectorError"] = $"Re-harvest selhal: {ex.Message}";
        }
        return RedirectToAction(nameof(Index));
    }
}
