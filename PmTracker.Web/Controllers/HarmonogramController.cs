using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.Security;
using IPmAuthorizationService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Plán 4 Feature C Task 7 — endpointy pro switch Auto/Ručně + výběr preferred kandidáta.
///
/// POST /Harmonogram/ToggleRezim    — přepne SkutecnostRezim (Auto ⇄ Manual) pro jednu HS0X_DELAY
///                                    hodnotu + spustí SyncZaznamAsync při přepnutí na Auto.
/// POST /Harmonogram/SelectCandidate — nastaví PreferredExterniOdkazId pro danou HS0X_DELAY +
///                                    spustí SyncZaznamAsync aby se metadata aktualizovala.
///
/// Autorizace: <see cref="PermissionKeys.RecordsScheduleEdit"/> scoped na projekt (manuální check —
/// endpoint nemá projektId v route).
/// Audit: <see cref="AuditActionType.Update"/> na <see cref="AuditEntityType.RecordSchedule"/>.
/// </summary>
[Authorize]
[Route("Harmonogram")]
public sealed class HarmonogramController : Controller
{
    private readonly PmTrackerDbContext _db;
    private readonly IHarmonogramSkutecnostSyncService _sync;
    private readonly IPmAuthorizationService _authz;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly TimeProvider _time;
    private readonly IAuditWriteService _audit;
    private readonly ILogger<HarmonogramController> _logger;

    public HarmonogramController(
        PmTrackerDbContext db,
        IHarmonogramSkutecnostSyncService sync,
        IPmAuthorizationService authz,
        ICurrentUserAccessor currentUser,
        TimeProvider time,
        IAuditWriteService audit,
        ILogger<HarmonogramController> logger)
    {
        _db = db;
        _sync = sync;
        _authz = authz;
        _currentUser = currentUser;
        _time = time;
        _audit = audit;
        _logger = logger;
    }

    public sealed record ToggleRezimRequest(int HodnotaId, SkutecnostRezimEnum Rezim);

    /// <summary>
    /// Přepne <see cref="ZaznamHarmonogramHodnotaEntity.SkutecnostRezim"/> mezi Auto a Manual.
    /// Při přepnutí na Auto spustí sync pro celý záznam (resolver přepočítá datum).
    /// Při přepnutí na Manual ponechá existující hodnotu (user dále edituje ručně).
    /// </summary>
    [HttpPost("ToggleRezim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRezim([FromBody] ToggleRezimRequest request, CancellationToken ct)
    {
        if (request is null || request.HodnotaId <= 0)
        {
            return BadRequest();
        }

        var row = await _db.ZaznamHarmonogramHodnoty
            .FirstOrDefaultAsync(h => h.Id == request.HodnotaId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return NotFound();
        }

        var projektId = await GetProjektIdAsync(row.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null)
        {
            return NotFound();
        }

        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false))
        {
            return Forbid();
        }

        if (row.SkutecnostRezim == request.Rezim)
        {
            return Ok(new { changed = false });
        }

        var previousRezim = row.SkutecnostRezim;
        var previousZdroj = row.SkutecnostZdroj;

        row.SkutecnostRezim = request.Rezim;
        if (request.Rezim == SkutecnostRezimEnum.Manual)
        {
            // Manual: pokud byl Automat, zachováme hodnotu ale flagneme Zdroj = Manual.
            if (row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
            {
                row.SkutecnostZdroj = SkutecnostZdrojEnum.Manual;
            }
        }
        else
        {
            // Auto: necháme sync-u resolve aktuální stav. Preferred zůstává.
            // Pokud dřív byl Manual/Historicka, sync (pokud najde kandidát) přepíše na Automat.
        }
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Audit log
        await _audit.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.RecordSchedule,
            row.Id.ToString(CultureInfo.InvariantCulture),
            BeforeState: new { Rezim = previousRezim, Zdroj = previousZdroj },
            AfterState: new { row.SkutecnostRezim, row.SkutecnostZdroj, Action = "toggle-rezim" }),
            ct).ConfigureAwait(false);

        // Při přepnutí na Auto spustíme sync (preferred zachováme, resolver si ho vezme).
        if (request.Rezim == SkutecnostRezimEnum.Auto)
        {
            try
            {
                await _sync.SyncZaznamAsync(row.ZaznamId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "HarmonogramController.ToggleRezim: sync selhal pro záznam {ZaznamId}.", row.ZaznamId);
            }
        }

        return Ok(new { changed = true, row.SkutecnostRezim, row.SkutecnostZdroj });
    }

    public sealed record SelectCandidateRequest(int HodnotaId, int? ExterniOdkazId);

    /// <summary>
    /// Nastaví (nebo clear-uje) <see cref="ZaznamHarmonogramHodnotaEntity.PreferredExterniOdkazId"/>.
    /// Caller předá buď validní <c>ExterniOdkazId</c> z dropdown kandidátů, nebo <c>null</c> pro clear.
    /// Po uložení spustí sync aby resolver aplikoval novou volbu.
    /// </summary>
    [HttpPost("SelectCandidate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectCandidate([FromBody] SelectCandidateRequest request, CancellationToken ct)
    {
        if (request is null || request.HodnotaId <= 0)
        {
            return BadRequest();
        }

        var row = await _db.ZaznamHarmonogramHodnoty
            .FirstOrDefaultAsync(h => h.Id == request.HodnotaId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return NotFound();
        }

        var projektId = await GetProjektIdAsync(row.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null)
        {
            return NotFound();
        }

        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false))
        {
            return Forbid();
        }

        if (row.SkutecnostRezim == SkutecnostRezimEnum.Manual)
        {
            return BadRequest(new { error = "Řádek je v režimu Manual — preferred kandidát nemá efekt." });
        }

        var previous = row.PreferredExterniOdkazId;
        row.PreferredExterniOdkazId = request.ExterniOdkazId;
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        await _audit.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.RecordSchedule,
            row.Id.ToString(CultureInfo.InvariantCulture),
            BeforeState: new { PreferredExterniOdkazId = previous },
            AfterState: new { row.PreferredExterniOdkazId, Action = "select-candidate" }),
            ct).ConfigureAwait(false);

        try
        {
            await _sync.SyncZaznamAsync(row.ZaznamId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "HarmonogramController.SelectCandidate: sync selhal pro záznam {ZaznamId}.", row.ZaznamId);
        }

        return Ok(new { row.PreferredExterniOdkazId });
    }

    // ---------- helpers ----------

    private async Task<int?> GetProjektIdAsync(int zaznamId, CancellationToken ct)
    {
        return await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(z => z.Id == zaznamId)
            .Select(z => (int?)z.ProjektId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    private async Task<bool> HasSchedulePermissionAsync(int projektId, CancellationToken ct)
    {
        var osobaId = _currentUser.OsobaId;
        if (!osobaId.HasValue) return false;
        return await _authz.HasPermissionAsync(
            osobaId.Value, PermissionKeys.RecordsScheduleEdit, projektId, null, ct).ConfigureAwait(false);
    }
}
