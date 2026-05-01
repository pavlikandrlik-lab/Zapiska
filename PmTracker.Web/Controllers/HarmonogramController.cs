using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Records;
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
    // Phase 7 (DESIGN-7-B): pending lock pre-check pro Manual→Auto toggle.
    private readonly IPendingScheduleProposalLockEvaluator _pendingLockEvaluator;

    public HarmonogramController(
        PmTrackerDbContext db,
        IHarmonogramSkutecnostSyncService sync,
        IPmAuthorizationService authz,
        ICurrentUserAccessor currentUser,
        TimeProvider time,
        IAuditWriteService audit,
        ILogger<HarmonogramController> logger,
        IPendingScheduleProposalLockEvaluator pendingLockEvaluator)
    {
        _db = db;
        _sync = sync;
        _authz = authz;
        _currentUser = currentUser;
        _time = time;
        _audit = audit;
        _logger = logger;
        _pendingLockEvaluator = pendingLockEvaluator;
    }

    /// <summary>
    /// Phase 7 (DESIGN-7-B): podporuje create-if-missing flow analog SelectCandidate.
    /// HodnotaId &gt; 0: classical (load existing row).
    /// ZaznamId + KrokPoradi: pokud HodnotaId nedostupný, server vytvoří DELAY row.
    /// </summary>
    public sealed record ToggleRezimRequest(
        int HodnotaId,
        SkutecnostRezimEnum Rezim,
        int? ZaznamId = null,
        int? KrokPoradi = null);

    /// <summary>
    /// Přepne <see cref="ZaznamHarmonogramHodnotaEntity.SkutecnostRezim"/> mezi Auto a Manual.
    /// Při přepnutí na Auto spustí sync pro celý záznam (resolver přepočítá datum).
    /// Při přepnutí na Manual ponechá existující hodnotu (user dále edituje ručně).
    /// </summary>
    [HttpPost("ToggleRezim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRezim([FromBody] ToggleRezimRequest request, CancellationToken ct)
    {
        if (request is null) return BadRequest();

        ZaznamHarmonogramHodnotaEntity? row = null;

        // Phase 7 (DESIGN-7-B + phantom bug 2 fix): create-if-missing analog SelectCandidate.
        // FIX 2026-05-01 (round 2 #11): auth-first ordering. Před EnsureDelayRowAsync
        // (= insert nového řádku) MUSÍ proběhnout authorization check, jinak může
        // unauthorized user pollute DB s prázdnými řádky v cizích projektech (data poisoning IDOR).
        if (request.HodnotaId > 0)
        {
            row = await _db.ZaznamHarmonogramHodnoty
                .FirstOrDefaultAsync(h => h.Id == request.HodnotaId, ct).ConfigureAwait(false);
            if (row is null) return NotFound();
        }
        else if (request.ZaznamId is int zaznamId && zaznamId > 0
                 && request.KrokPoradi is int krokPoradi && krokPoradi > 0)
        {
            var zaznam = await _db.ProjektoveZaznamy.AsNoTracking()
                .FirstOrDefaultAsync(z => z.Id == zaznamId, ct).ConfigureAwait(false);
            if (zaznam is null) return NotFound();

            // AUTH GUARD před create — prevent data poisoning IDOR.
            if (!await HasSchedulePermissionAsync(zaznam.ProjektId, ct).ConfigureAwait(false))
            {
                return Forbid();
            }

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

            // Race-safe insert (TOCTOU bug fix).
            row = await EnsureDelayRowAsync(zaznamId, delayTypId.Value, ct).ConfigureAwait(false);
        }
        else
        {
            return BadRequest(new { error = "Předej HodnotaId nebo ZaznamId+KrokPoradi." });
        }

        var projektId = await GetProjektIdAsync(row.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();

        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false))
        {
            return Forbid();
        }

        // Phase 7 (DESIGN-7-B) + FIX 2026-05-01 (#6): pending lock pre-check pro JAKÝKOLI toggle.
        // Předtím check pouze pro Manual→Auto. Ale Auto→Manual při existujícím pending na ten krok
        // znamená, že user "obchází" návrh (změna rezimu při čekajícím návrhu = race / bypass).
        // Pending = univerzální guard — žádný toggle během pendingu.
        if (row.SkutecnostRezim != request.Rezim)
        {
            var lockState = await _pendingLockEvaluator.EvaluateAsync(row.ZaznamId, ct).ConfigureAwait(false);
            if (lockState.HasPendingProposal && lockState.LockedManualKrokKeys is not null)
            {
                var krokKey = await _db.CiselnikHarmonogramTypu.AsNoTracking()
                    .Where(t => t.Id == row.TypId)
                    .Select(t => (Guid?)t.KrokKey)
                    .FirstOrDefaultAsync(ct).ConfigureAwait(false);
                if (krokKey.HasValue && lockState.LockedManualKrokKeys.Contains(krokKey.Value))
                {
                    return BadRequest(new
                    {
                        error = $"Pending návrh #{lockState.ProposalId} blokuje přepnutí rezimu kroku. Vyřeš návrh nejdříve."
                    });
                }
            }
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
        // FIX 2026-05-01 (round 2 #13): sync exception propagation — místo silently log+200 OK
        // vrátíme partial-success indikátor (changed=true, syncFailed=true). UI zobrazí warning,
        // user vidí, že rezim přepnut, ale auto-fill data nejsou refresh.
        bool syncFailed = false;
        string? syncFailReason = null;
        if (request.Rezim == SkutecnostRezimEnum.Auto)
        {
            try
            {
                await _sync.SyncZaznamAsync(row.ZaznamId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                syncFailed = true;
                syncFailReason = ex.Message;
                _logger.LogWarning(ex,
                    "HarmonogramController.ToggleRezim: sync selhal pro záznam {ZaznamId}.", row.ZaznamId);
            }
        }

        return Ok(new { changed = true, row.SkutecnostRezim, row.SkutecnostZdroj, syncFailed, syncFailReason });
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

            // FIX 2026-05-01 (round 2 #11): auth-first ordering. Prevent data poisoning IDOR.
            if (!await HasSchedulePermissionAsync(zaznam.ProjektId, ct).ConfigureAwait(false))
            {
                return Forbid();
            }

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

            // Race-safe insert (TOCTOU bug fix) + DESIGN-10-A NULL semantika.
            row = await EnsureDelayRowAsync(zaznamId, delayTypId.Value, ct).ConfigureAwait(false);
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

        // FIX 2026-05-01 (round 5 #1): pending lock pre-check pro SelectCandidate.
        // SelectCandidate změní PreferredExterniOdkazId + spustí SyncZaznamAsync, který přepíše
        // HodnotaInt v DB. Pokud existuje pending návrh na schedule pro tento krok, výběr by
        // byl skrytý bypass návrhu (user změní auto-fill data, která jsou v návrhu zamčená).
        // Pending = univerzální guard — žádná auto-fill změna během pending návrhu.
        var lockState = await _pendingLockEvaluator.EvaluateAsync(row.ZaznamId, ct).ConfigureAwait(false);
        if (lockState.HasPendingProposal && lockState.LockedManualKrokKeys is not null)
        {
            var krokKey = await _db.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(t => t.Id == row.TypId)
                .Select(t => (Guid?)t.KrokKey)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (krokKey.HasValue && lockState.LockedManualKrokKeys.Contains(krokKey.Value))
            {
                return BadRequest(new
                {
                    error = $"Pending návrh #{lockState.ProposalId} blokuje výběr kandidáta pro tento krok. Vyřeš návrh nejdříve."
                });
            }
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

        // FIX 2026-05-01 (round 2 #13): sync exception propagation — UI signal partial success.
        bool syncFailed = false;
        string? syncFailReason = null;
        try
        {
            await _sync.SyncZaznamAsync(row.ZaznamId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            syncFailed = true;
            syncFailReason = ex.Message;
            _logger.LogWarning(ex,
                "HarmonogramController.SelectCandidate: sync selhal pro záznam {ZaznamId}.", row.ZaznamId);
        }

        return Ok(new { row.PreferredExterniOdkazId, syncFailed, syncFailReason });
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

    /// <summary>
    /// FIX 2026-05-01 — race-safe create-if-missing pro DELAY row.
    /// Pattern: try-load existing, if missing try-insert s catch DbUpdateException
    /// (TOCTOU window between check and insert mezi paralelními requesty). Pokud insert selže
    /// kvůli unique violation, re-load existing — concurrent caller už insert provedl.
    /// </summary>
    private async Task<ZaznamHarmonogramHodnotaEntity> EnsureDelayRowAsync(
        int zaznamId, int delayTypId, CancellationToken ct)
    {
        var existing = await _db.ZaznamHarmonogramHodnoty
            .FirstOrDefaultAsync(h => h.ZaznamId == zaznamId && h.TypId == delayTypId, ct)
            .ConfigureAwait(false);
        if (existing is not null) return existing;

        var row = new ZaznamHarmonogramHodnotaEntity
        {
            ZaznamId = zaznamId,
            TypId = delayTypId,
            HodnotaInt = null, // DESIGN-10-A: NULL = "krok nenastal" (default insert state).
            UpdatedAt = _time.GetUtcNow().UtcDateTime,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
        };
        _db.ZaznamHarmonogramHodnoty.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return row;
        }
        catch (DbUpdateException)
        {
            // Concurrent insert vyhrál — odstaníme tracked entity + reload winner.
            // FIX 2026-05-01 (round 6 #2): reload jako tracked aby caller mohl modifikovat
            // (volající nastaví SkutecnostRezim/PreferredExterniOdkazId a opět SaveChangesAsync).
            // Záměr: detach loser, reload winner v čistém tracking stavu.
            _db.Entry(row).State = EntityState.Detached;
            var winner = await _db.ZaznamHarmonogramHodnoty
                .FirstOrDefaultAsync(h => h.ZaznamId == zaznamId && h.TypId == delayTypId, ct)
                .ConfigureAwait(false);
            return winner ?? throw new InvalidOperationException(
                $"EnsureDelayRowAsync race resolution selhala — řádek pro (zaznamId={zaznamId}, typId={delayTypId}) nenalezen.");
        }
    }
}
