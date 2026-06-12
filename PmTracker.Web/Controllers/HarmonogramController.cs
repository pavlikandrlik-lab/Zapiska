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
/// Datum-model (2026-06-12) — endpointy pro switch Auto/Ručně + výběr preferred kandidáta,
/// operující nad <see cref="ZaznamHarmonogramKrokEntity"/> (identifikace přes ZaznamId + Poradi).
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

    public sealed record ToggleRezimRequest(int ZaznamId, int Poradi, SkutecnostRezimEnum Rezim);

    [HttpPost("ToggleRezim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRezim([FromBody] ToggleRezimRequest request, CancellationToken ct)
    {
        if (request is null || request.ZaznamId <= 0 || request.Poradi is < 1 or > 10)
        {
            return BadRequest(new { error = "Předej ZaznamId a Poradi (1–10)." });
        }

        var projektId = await GetProjektIdAsync(request.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();
        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false)) return Forbid();

        if (await IsScheduleLockedAsync(request.ZaznamId, ct).ConfigureAwait(false) is { } lockMsg)
        {
            return BadRequest(new { error = lockMsg });
        }

        var row = await EnsureKrokRowAsync(request.ZaznamId, request.Poradi, ct).ConfigureAwait(false);
        var currentRezim = (SkutecnostRezimEnum)row.SkutecnostRezim;
        if (currentRezim == request.Rezim)
        {
            return Ok(new { changed = false });
        }

        var previousRezim = currentRezim;
        var previousZdroj = (SkutecnostZdrojEnum)row.SkutecnostZdroj;

        row.SkutecnostRezim = (byte)request.Rezim;
        if (request.Rezim == SkutecnostRezimEnum.Manual
            && (SkutecnostZdrojEnum)row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
        {
            row.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual;
        }
        row.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        await _audit.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.RecordSchedule,
            row.Id.ToString(CultureInfo.InvariantCulture),
            BeforeState: new { Rezim = previousRezim, Zdroj = previousZdroj },
            AfterState: new { Rezim = request.Rezim, Action = "toggle-rezim" }),
            ct).ConfigureAwait(false);

        var (syncFailed, syncFailReason) = request.Rezim == SkutecnostRezimEnum.Auto
            ? await TrySyncAsync(request.ZaznamId, ct).ConfigureAwait(false)
            : (false, null);

        return Ok(new { changed = true, Rezim = request.Rezim, syncFailed, syncFailReason });
    }

    public sealed record SelectCandidateRequest(int ZaznamId, int Poradi, int? ExterniOdkazId);

    [HttpPost("SelectCandidate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectCandidate([FromBody] SelectCandidateRequest request, CancellationToken ct)
    {
        if (request is null || request.ZaznamId <= 0 || request.Poradi is < 1 or > 10)
        {
            return BadRequest(new { error = "Předej ZaznamId a Poradi (1–10)." });
        }

        var projektId = await GetProjektIdAsync(request.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();
        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false)) return Forbid();

        if (await IsScheduleLockedAsync(request.ZaznamId, ct).ConfigureAwait(false) is { } lockMsg)
        {
            return BadRequest(new { error = lockMsg });
        }

        var row = await EnsureKrokRowAsync(request.ZaznamId, request.Poradi, ct).ConfigureAwait(false);
        if ((SkutecnostRezimEnum)row.SkutecnostRezim == SkutecnostRezimEnum.Manual)
        {
            return BadRequest(new { error = "Krok je v režimu Manual — preferred kandidát nemá efekt." });
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

        var (syncFailed, syncFailReason) = await TrySyncAsync(request.ZaznamId, ct).ConfigureAwait(false);
        return Ok(new { row.PreferredExterniOdkazId, syncFailed, syncFailReason });
    }

    public sealed record BulkSetRezimRequest(int ZaznamId, string Rezim);

    [HttpPost("BulkSetRezim")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSetRezim([FromBody] BulkSetRezimRequest request, CancellationToken ct)
    {
        if (request is null || request.ZaznamId <= 0)
        {
            return BadRequest(new { error = "Chybí ZaznamId v requestu." });
        }
        if (!Enum.TryParse<SkutecnostRezimEnum>(request.Rezim, ignoreCase: true, out var targetRezim))
        {
            return BadRequest(new { error = $"Neplatný rezim '{request.Rezim}'. Povoleno: Auto, Manual." });
        }

        var projektId = await GetProjektIdAsync(request.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();
        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false)) return Forbid();

        if (await IsScheduleLockedAsync(request.ZaznamId, ct).ConfigureAwait(false) is { } lockMsg)
        {
            return BadRequest(new { error = lockMsg });
        }

        // Bulk přepíná jen auto-eligible kroky (manuální 2/5/8/9 jsou vždy ručně).
        var manualPoradi = HarmonogramKroky.Vse.Where(k => k.JeManualni).Select(k => (byte)k.Poradi).ToHashSet();
        var rows = await _db.ZaznamHarmonogramKroky
            .Where(k => k.ZaznamId == request.ZaznamId)
            .ToListAsync(ct).ConfigureAwait(false);
        var eligible = rows.Where(r => !manualPoradi.Contains(r.Poradi)).ToList();

        if (eligible.Count == 0)
        {
            return Ok(new { changed = 0, message = "Žádné kroky k přepnutí." });
        }

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        int changed = 0;
        foreach (var row in eligible)
        {
            if ((SkutecnostRezimEnum)row.SkutecnostRezim == targetRezim) continue;
            row.SkutecnostRezim = (byte)targetRezim;
            if (targetRezim == SkutecnostRezimEnum.Manual
                && (SkutecnostZdrojEnum)row.SkutecnostZdroj == SkutecnostZdrojEnum.Automat)
            {
                row.SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Manual;
            }
            row.UpdatedAt = nowUtc;
            changed++;
        }

        if (changed == 0)
        {
            return Ok(new { changed, message = "Všechny kroky už jsou v požadovaném rezimu." });
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        await _audit.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
            AuditActionType.Update,
            AuditEntityType.RecordSchedule,
            request.ZaznamId.ToString(CultureInfo.InvariantCulture),
            BeforeState: new { Action = "bulk-set-rezim-before" },
            AfterState: new { Rezim = targetRezim, ChangedRows = changed }),
            ct).ConfigureAwait(false);

        var (syncFailed, syncFailReason) = targetRezim == SkutecnostRezimEnum.Auto
            ? await TrySyncAsync(request.ZaznamId, ct).ConfigureAwait(false)
            : (false, null);

        return Ok(new { changed, Rezim = targetRezim, syncFailed, syncFailReason });
    }

    public sealed record PreviewSyncRequest(int ZaznamId);

    [HttpPost("PreviewSync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewSync([FromBody] PreviewSyncRequest request, CancellationToken ct)
    {
        if (request is null || request.ZaznamId <= 0) return BadRequest();

        var projektId = await GetProjektIdAsync(request.ZaznamId, ct).ConfigureAwait(false);
        if (projektId is null) return NotFound();
        if (!await HasSchedulePermissionAsync(projektId.Value, ct).ConfigureAwait(false)) return Forbid();

        var plan = await _sync.ComputePlanAsync(request.ZaznamId, ct).ConfigureAwait(false);
        return Ok(plan);
    }

    // ---------- helpers ----------

    private async Task<int?> GetProjektIdAsync(int zaznamId, CancellationToken ct)
        => await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(z => z.Id == zaznamId)
            .Select(z => (int?)z.ProjektId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

    private async Task<bool> HasSchedulePermissionAsync(int projektId, CancellationToken ct)
    {
        var osobaId = _currentUser.OsobaId;
        if (!osobaId.HasValue) return false;
        return await _authz.HasPermissionAsync(
            osobaId.Value, PermissionKeys.RecordsScheduleEdit, projektId, null, ct).ConfigureAwait(false);
    }

    /// <summary>Vrací chybovou hlášku pokud pending návrh zamyká schedule, jinak null.</summary>
    private async Task<string?> IsScheduleLockedAsync(int zaznamId, CancellationToken ct)
    {
        var lockState = await _pendingLockEvaluator.EvaluateAsync(zaznamId, ct).ConfigureAwait(false);
        return lockState.HasPendingProposal && lockState.LocksSchedule
            ? $"Pending návrh #{lockState.ProposalId} blokuje změnu skutečnosti. Vyřeš návrh nejdříve."
            : null;
    }

    private async Task<(bool failed, string? reason)> TrySyncAsync(int zaznamId, CancellationToken ct)
    {
        try
        {
            await _sync.SyncZaznamAsync(zaznamId, ct).ConfigureAwait(false);
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HarmonogramController: sync selhal pro záznam {ZaznamId}.", zaznamId);
            return (true, ex.Message);
        }
    }

    /// <summary>Race-safe create-if-missing krok řádku (ZaznamId + Poradi).</summary>
    private async Task<ZaznamHarmonogramKrokEntity> EnsureKrokRowAsync(int zaznamId, int poradi, CancellationToken ct)
    {
        var existing = await _db.ZaznamHarmonogramKroky
            .FirstOrDefaultAsync(k => k.ZaznamId == zaznamId && k.Poradi == (byte)poradi, ct).ConfigureAwait(false);
        if (existing is not null) return existing;

        var row = new ZaznamHarmonogramKrokEntity
        {
            ZaznamId = zaznamId,
            Poradi = (byte)poradi,
            SkutecnostDatum = null,
            SkutecnostRezim = (byte)SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = (byte)SkutecnostZdrojEnum.Neznamo,
            UpdatedAt = _time.GetUtcNow().UtcDateTime
        };
        _db.ZaznamHarmonogramKroky.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return row;
        }
        catch (DbUpdateException)
        {
            _db.Entry(row).State = EntityState.Detached;
            var winner = await _db.ZaznamHarmonogramKroky
                .FirstOrDefaultAsync(k => k.ZaznamId == zaznamId && k.Poradi == (byte)poradi, ct).ConfigureAwait(false);
            return winner ?? throw new InvalidOperationException(
                $"EnsureKrokRowAsync race resolution selhala — (zaznamId={zaznamId}, poradi={poradi}).");
        }
    }
}
