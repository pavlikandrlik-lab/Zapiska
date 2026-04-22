using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.Vyjadreni;
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

    public VyjadreniModalController(
        PmTrackerDbContext db,
        IVyjadreniModalViewModelBuilder builder,
        IVyjadreniHarvestService harvest,
        IPmAuthorizationService authz,
        ICurrentUserAccessor currentUser,
        TimeProvider time,
        ILogger<VyjadreniModalController> logger)
    {
        _db = db;
        _builder = builder;
        _harvest = harvest;
        _authz = authz;
        _currentUser = currentUser;
        _time = time;
        _logger = logger;
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

        // Projekt dohledáme před autorizací (scope je per-project).
        var projektId = await _db.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == zaznamId)
            .Select(x => (int?)x.ProjektId)
            .FirstOrDefaultAsync(ct);
        if (projektId is null) return NotFound();

        // Read modal může kdokoli, kdo má na projekt records.edit (editor edituje záznam).
        // Pokud nemá edit, může stále číst jen když má obecný project read — zjednodušení:
        // required records.edit pro chat modal, protože z definice je modal editační UX.
        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, projektId.Value, null, ct))
        {
            return Forbid();
        }

        var vm = await _builder.BuildAsync(externiOdkazId, zaznamId, canEdit: true, ct).ConfigureAwait(false);
        if (vm is null) return NotFound();
        return PartialView("~/Views/Vyjadreni/_ChatModal.cshtml", vm);
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

        var eoExists = await _db.ZaznamExterniOdkazy.AsNoTracking()
            .AnyAsync(x => x.Id == req.ExterniOdkazId && x.ZaznamId == req.ZaznamId, ct);
        if (!eoExists) return NotFound(new { Error = "Externí odkaz nenalezen nebo nepatří k záznamu." });

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
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReHarvest selhal pro externí odkaz {Id}.", externiOdkazId);
            return StatusCode(500, new { Error = "Re-harvest selhal, viz log." });
        }
    }
}
