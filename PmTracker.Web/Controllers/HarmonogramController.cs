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

    /// <summary>
    /// Výběr preferred kandidáta. Klient posílá buď <c>HodnotaId</c> (HS0X_DELAY row existuje)
    /// NEBO <c>ZaznamId + KrokPoradi</c> (řádek neexistuje, endpoint ho vytvoří — Feature C gap #3
    /// create-if-missing aby šlo dropdown použít i pro kroky bez dosud zapsané skutečnosti).
    /// </summary>
    public sealed record SelectCandidateRequest(
        int HodnotaId,
        int? ExterniOdkazId,
        int? ZaznamId = null,
        int? KrokPoradi = null);

    /// <summary>
    /// Nastaví (nebo clear-uje) <see cref="ZaznamHarmonogramHodnotaEntity.PreferredExterniOdkazId"/>.
    /// Pokud HS0X_DELAY row pro krok neexistuje, vytvoří ho s default hodnotami
    /// (HodnotaInt=0, Rezim=Auto, Zdroj=Neznamo) a pak na něj nastaví preferred.
    /// Po uložení spustí sync aby resolver aplikoval novou volbu + spočítal HodnotaInt.
    /// </summary>
    [HttpPost("SelectCandidate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectCandidate([FromBody] SelectCandidateRequest request, CancellationToken ct)
    {
        if (request is null) return BadRequest();

        ZaznamHarmonogramHodnotaEntity? row = null;
        if (request.HodnotaId > 0)
        {
            row = await _db.ZaznamHarmonogramHodnoty
                .FirstOrDefaultAsync(h => h.Id == request.HodnotaId, ct).ConfigureAwait(false);
            if (row is null)
            {
                return NotFound();
            }
        }
        else if (request.ZaznamId is int zaznamId && zaznamId > 0
                 && request.KrokPoradi is int krokPoradi && krokPoradi > 0)
        {
            // Create-if-missing flow (Feature C gap #3):
            // najdi HS0X_DELAY TypId pro daný krok + vytvoř row s defaults.
            var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == zaznamId, ct).ConfigureAwait(false);
            if (zaznam is null) return NotFound();

            var delayTypId = await _db.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(t => t.SablonaVerze == zaznam.HarmonogramSablonaVerze
                         && t.JeZpozdeni
                         && t.KrokPoradi == krokPoradi)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (delayTypId is null)
            {
                return NotFound(new { error = $"Krok {krokPoradi} neexistuje v schématu záznamu." });
            }

            // Check existing row (concurrent create guard)
            row = await _db.ZaznamHarmonogramHodnoty
                .FirstOrDefaultAsync(h => h.ZaznamId == zaznamId && h.TypId == delayTypId.Value, ct)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = new ZaznamHarmonogramHodnotaEntity
                {
                    ZaznamId = zaznamId,
                    TypId = delayTypId.Value,
                    HodnotaInt = 0,
                    UpdatedAt = _time.GetUtcNow().UtcDateTime,
                    SkutecnostRezim = SkutecnostRezimEnum.Auto,
                    SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
                };
                _db.ZaznamHarmonogramHodnoty.Add(row);
                // Save aby row dostal Id před dalším update (audit log používá row.Id)
                await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }
        else
        {
            return BadRequest(new { error = "Musíš předat buď HodnotaId, nebo ZaznamId + KrokPoradi." });
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

    public sealed record PreviewSyncRequest(int ZaznamId);

    /// <summary>
    /// DESIGN-9-C (2026-05-01) — staging endpoint pro UI pre-fetch flow.
    /// Vrací plán zamýšlených změn pro daný záznam (ComputePlanAsync), NIC nezapisuje do DB.
    /// JS strana drží plán v sessionStorage; commit přes Save form (RecordService) nebo
    /// existing Toggle/SelectCandidate triggery.
    /// </summary>
    [HttpPost("PreviewSync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewSync([FromBody] PreviewSyncRequest request, CancellationToken ct)
    {
        if (request is null || request.ZaznamId <= 0)
        {
            return BadRequest();
        }

        var projektId = await GetProjektIdAsync(request.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null)
        {
            return NotFound();
        }

        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false))
        {
            return Forbid();
        }

        var plan = await _sync.ComputePlanAsync(request.ZaznamId, ct).ConfigureAwait(false);
        return Ok(plan);
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
