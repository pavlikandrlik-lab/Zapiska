using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.Vyjadreni;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;
using IPmAuthorizationService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Endpointy pro chat modal (Plán C).
/// - GET /Vyjadreni/Modal → vrací Razor partial s view-modelem
/// - POST /Vyjadreni/HarmonogramVazba/Create → přiřadí bublinu ke kroku (Manual source)
/// - POST /Vyjadreni/HarmonogramVazba/Delete → soft-delete existující vazby
/// - POST /Vyjadreni/ReHarvest → admin akce, synchronně přeharvestuje jednu externí vazbu
/// </summary>
/// <remarks>
/// Autorizace: records.edit scoped na <c>projektId</c> z body (manuální check
/// přes <see cref="IPmAuthorizationService"/>, policy attribute by nefungoval —
/// endpoint nemá projektId v route). ReHarvest vyžaduje <c>settings.manage</c>.
/// </remarks>
[Authorize]
[Route("Vyjadreni")]
public sealed class VyjadreniModalController : Controller
{
    private readonly PmTrackerDbContext _db;
    private readonly IVyjadreniModalViewModelBuilder _builder;
    private readonly IVyjadreniHarvestService _harvest;
    private readonly IPmAuthorizationService _authz;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly TimeProvider _time;
    private readonly ILogger<VyjadreniModalController> _logger;
    private readonly IAuditWriteService _auditWriteService;

    public VyjadreniModalController(
        PmTrackerDbContext db,
        IVyjadreniModalViewModelBuilder builder,
        IVyjadreniHarvestService harvest,
        IPmAuthorizationService authz,
        ICurrentUserAccessor currentUser,
        TimeProvider time,
        ILogger<VyjadreniModalController> logger,
        IAuditWriteService auditWriteService)
    {
        _db = db;
        _builder = builder;
        _harvest = harvest;
        _authz = authz;
        _currentUser = currentUser;
        _time = time;
        _logger = logger;
        _auditWriteService = auditWriteService;
    }

    [HttpGet("Modal")]
    public async Task<IActionResult> Modal(int externiOdkazId, int zaznamId, CancellationToken ct)
    {
        if (externiOdkazId <= 0 || zaznamId <= 0)
        {
            return BadRequest();
        }

        var osobaId = _currentUser.OsobaId;
        if (osobaId is null) return Forbid();

        // Review finding M-R2-1: ověř, že externiOdkazId patří k zaznamId PŘED harvestem.
        // Jinak attacker s records.edit na projekt A může vynucovat harvest tiketu projektu B
        // (DoS amplifier + cross-project LastHarvestedAt mutace).
        var target = await (from eo in _db.ZaznamExterniOdkazy.AsNoTracking()
                            join z in _db.ProjektoveZaznamy.AsNoTracking() on eo.ZaznamId equals z.Id
                            where eo.Id == externiOdkazId && eo.ZaznamId == zaznamId
                            select new { ProjektId = z.ProjektId })
                           .FirstOrDefaultAsync(ct);
        if (target is null) return NotFound();

        // Read modal může kdokoli, kdo má na projekt records.edit (editor edituje záznam).
        // Pokud nemá edit, může stále číst jen když má obecný project read — zjednodušení:
        // required records.edit pro chat modal, protože z definice je modal editační UX.
        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, target.ProjektId, null, ct))
        {
            return Forbid();
        }

        // T3 — direct sync při otevření modalu. User čeká. Per-ticket lock + fingerprint
        // minimalizuje práci proti HOT DB; pokud fingerprint říká no-change, drill se
        // preskočí (žádný HOT_VYJADRENI fetch). Spec §8.6.
        try
        {
            await _harvest.HarvestSingleTicketAsync(externiOdkazId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "T3 direct sync selhal pro externí odkaz {Id} — zobrazím cached data.",
                externiOdkazId);
            // Nefail modal open — zobrazíme cached data.
        }

        var vm = await _builder.BuildAsync(externiOdkazId, zaznamId, canEdit: true, ct).ConfigureAwait(false);
        if (vm is null) return NotFound();
        return PartialView("~/Views/Vyjadreni/_ChatModal.cshtml", vm);
    }

    /// <summary>
    /// T6 — manuální refresh z chat modalu nebo z karty externí vazby.
    /// User klikne 🔄 → endpoint synchronně zavolá HarvestSingleTicketAsync, která
    /// projde fingerprint checkem a případně drillne. Per-externiOdkazId 1-min
    /// hard floor via IMemoryCache v front-endu/shared middleware je TBD; pro
    /// teď spoléháme na queue dedup (T2/T5/T7/T8) a per-ticket lock (Task 6).
    /// </summary>
    [HttpPost("Refresh")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refresh([FromForm] int externiOdkazId, [FromForm] int projektId, CancellationToken ct)
    {
        if (externiOdkazId <= 0 || projektId <= 0) return BadRequest();

        var osobaId = _currentUser.OsobaId;
        if (osobaId is null) return Forbid();

        // Review finding M-R2-1: ověř, že externiOdkazId skutečně patří k projektId
        // PŘED harvestem. Jinak attacker s records.edit na projekt A může přes cizí
        // externiOdkazId vynucovat harvest jiného projektu (DoS + cross-project mutace).
        var ownerProjektId = await (from eo in _db.ZaznamExterniOdkazy.AsNoTracking()
                                    join z in _db.ProjektoveZaznamy.AsNoTracking() on eo.ZaznamId equals z.Id
                                    where eo.Id == externiOdkazId
                                    select (int?)z.ProjektId)
                                   .FirstOrDefaultAsync(ct);
        if (ownerProjektId is null) return NotFound();
        if (ownerProjektId.Value != projektId) return Forbid();

        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, projektId, null, ct))
        {
            return Forbid();
        }

        try
        {
            var result = await _harvest.HarvestSingleTicketAsync(externiOdkazId, ct).ConfigureAwait(false);
            return Ok(new
            {
                Success = true,
                result.Fetched,
                result.Created,
                result.Superseded,
                result.Skipped,
                result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "T6 refresh selhal pro externí odkaz {Id}.", externiOdkazId);
            return StatusCode(500, new { Error = "Refresh selhal, viz log." });
        }
    }

    [HttpPost("HarmonogramVazba/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVazba([FromBody] CreateVazbaRequest req, CancellationToken ct)
    {
        var osobaId = _currentUser.OsobaId;
        if (osobaId is null) return Forbid();
        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, req.ProjektId, null, ct))
        {
            return Forbid();
        }

        // Review finding S-4: IDOR — ověř, že externí odkaz nejen existuje a patří k zaznamId,
        // ale že záznam skutečně patří do req.ProjektId (jinak útočník s records.edit na projekt A
        // může mutovat vazbu v projektu B). Join explicitní přes _db.ProjektoveZaznamy —
        // ZaznamExterniOdkazEntity nemá navigation property.
        var eoRow = await (from eo in _db.ZaznamExterniOdkazy.AsNoTracking()
                           join z in _db.ProjektoveZaznamy.AsNoTracking() on eo.ZaznamId equals z.Id
                           where eo.Id == req.ExterniOdkazId && eo.ZaznamId == req.ZaznamId
                           select new { OwnerProjektId = z.ProjektId })
                          .FirstOrDefaultAsync(ct);
        if (eoRow is null) return NotFound(new { Error = "Externí odkaz nenalezen nebo nepatří k záznamu." });
        if (eoRow.OwnerProjektId != req.ProjektId) return Forbid();

        // Superseduj Active binding pro (zaznamId, krokKey)
        var existing = await _db.VyjadreniVazby
            .Where(x => x.ZaznamId == req.ZaznamId
                     && x.KrokKey == req.KrokKey
                     && x.Stav == (byte)VazbaStav.Active)
            .ToListAsync(ct);

        var nowUtc = _time.GetUtcNow().UtcDateTime;
        foreach (var e in existing)
        {
            e.Stav = (byte)VazbaStav.Superseded;
        }

        var entity = new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = req.ZaznamId,
            KrokKey = req.KrokKey,
            ExterniOdkazId = req.ExterniOdkazId,
            HotVyjadreniId = req.HotVyjadreniId,
            DatumVyjadreni = req.DatumVyjadreni,
            Source = (byte)VazbaSource.Manual,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = nowUtc,
            CreatedByOsobaId = osobaId
        };
        _db.VyjadreniVazby.Add(entity);
        await _db.SaveChangesAsync(ct);

        return Ok(new { VazbaId = entity.Id, Superseded = existing.Count });
    }

    [HttpPost("HarmonogramVazba/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVazba([FromBody] DeleteVazbaRequest req, CancellationToken ct)
    {
        var osobaId = _currentUser.OsobaId;
        if (osobaId is null) return Forbid();
        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, req.ProjektId, null, ct))
        {
            return Forbid();
        }

        var vazba = await _db.VyjadreniVazby.FirstOrDefaultAsync(x => x.Id == req.VazbaId, ct);
        if (vazba is null) return NotFound();
        if (vazba.Stav == (byte)VazbaStav.Deleted) return Ok(new { AlreadyDeleted = true });

        // Review finding S-4: IDOR — ověř, že vazba.ZaznamId patří do req.ProjektId.
        var ownerProjektId = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == vazba.ZaznamId)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct);
        if (ownerProjektId is null) return NotFound();
        if (ownerProjektId.Value != req.ProjektId) return Forbid();

        vazba.Stav = (byte)VazbaStav.Deleted;
        vazba.DeletedAt = _time.GetUtcNow().UtcDateTime;
        vazba.DeletedByOsobaId = osobaId;
        await _db.SaveChangesAsync(ct);

        return Ok();
    }

    [HttpPost("ReHarvest")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "permission:settings.manage")]
    public async Task<IActionResult> ReHarvest([FromForm] int externiOdkazId, CancellationToken ct)
    {
        if (externiOdkazId <= 0) return BadRequest();

        try
        {
            var result = await _harvest.ReHarvestTicketAsync(externiOdkazId, ct).ConfigureAwait(false);

            // Review finding S-3: audit destruktivní admin akce.
            await _auditWriteService.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
                AuditActionType.Update,
                AuditEntityType.SdExterniOdkaz,
                externiOdkazId.ToString(CultureInfo.InvariantCulture),
                BeforeState: null,
                AfterState: new
                {
                    Action = "reharvest",
                    Source = "VyjadreniModalController",
                    result.Fetched,
                    result.Created,
                    result.Superseded,
                    result.Skipped
                }), ct).ConfigureAwait(false);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReHarvest selhal pro externí odkaz {Id}.", externiOdkazId);
            // Review finding M-R2-3: audit entry i při selhání, aby byla stopa pokusu.
            try
            {
                await _auditWriteService.WriteAsync(_currentUser.OsobaId, new AuditWriteEntry(
                    AuditActionType.Update,
                    AuditEntityType.SdExterniOdkaz,
                    externiOdkazId.ToString(CultureInfo.InvariantCulture),
                    BeforeState: null,
                    AfterState: new
                    {
                        Action = "reharvest.failed",
                        Source = "VyjadreniModalController",
                        TraceId = HttpContext.TraceIdentifier
                    }), ct).ConfigureAwait(false);
            }
            catch (Exception auditEx)
            {
                _logger.LogWarning(auditEx, "ReHarvest: zápis auditu selhání selhal pro {Id}.", externiOdkazId);
            }
            // Review finding S-3: nelogovat exception.Message do odpovědi, jen trace id.
            return StatusCode(500, new { Error = $"Re-harvest selhal. TraceId: {HttpContext.TraceIdentifier}" });
        }
    }
}
