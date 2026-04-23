using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Controllers;

[Authorize]
[Route("vyzvy")]
public sealed class VyzvyController : BaseController
{
    private readonly IVyzvaService _vyzvaService;
    private readonly TimeProvider _timeProvider;

    public VyzvyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IVyzvaService vyzvaService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _vyzvaService = vyzvaService;
        _timeProvider = timeProvider;
    }

    public sealed class ZaloztRequest { public int ProjektId { get; set; } }
    public sealed class ZmenitStavRequest { public int VyzvaId { get; set; } public string NovyStav { get; set; } = string.Empty; }
    public sealed class SetZaradidRequest { public int ExterniOdkazId { get; set; } public bool Zaradit { get; set; } }
    public sealed class PrerditRequest { public int ExterniOdkazId { get; set; } public int? CilovaVyzvaId { get; set; } }

    [HttpPost("zalozit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Zalozit([FromForm] ZaloztRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, errorCode = "ValidationError", message = "Chybí požadované údaje." });

        // Per-action redesign 2026-04-23: vyzvy.create (dříve jen CanAccessProject).
        if (!CurrentUserContext.HasPermission(PermissionKeys.VyzvyCreate, request.ProjektId)) return Forbid();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _vyzvaService.ZaloztVyzvuZBufferuAsync(
            request.ProjektId, CurrentUserContext.OsobaId, now, ct);

        return result switch
        {
            VyzvaResult<VyzvaDetail>.Ok => Ok(new { success = true, reload = true }),
            VyzvaResult<VyzvaDetail>.Fail f => Ok(new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message }),
            _ => StatusCode(500),
        };
    }

    [HttpPost("zmenit-stav")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZmenitStav([FromForm] ZmenitStavRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, errorCode = "ValidationError", message = "Chybí požadované údaje." });

        if (!Enum.TryParse<VyzvaStav>(request.NovyStav, ignoreCase: false, out var stav))
            return BadRequest(new { success = false, errorCode = "InvalidStateTransition", message = "Neplatný stav." });

        var vyzva = await _vyzvaService.GetVyzvaAsync(request.VyzvaId, ct);
        if (vyzva == null) return NotFound();
        // Per-action redesign 2026-04-23: vyzvy.state.change (dříve jen CanAccessProject).
        if (!CurrentUserContext.HasPermission(PermissionKeys.VyzvyStateChange, vyzva.ProjektId)) return Forbid();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var result = await _vyzvaService.ZmenitStavAsync(
            request.VyzvaId, stav, CurrentUserContext.OsobaId, now, ct);

        return result switch
        {
            VyzvaResult<VyzvaDetail>.Ok => Ok(new { success = true, reload = true }),
            VyzvaResult<VyzvaDetail>.Fail f => Ok(new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message }),
            _ => StatusCode(500),
        };
    }

    [HttpPost("set-zaradid")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetZaradid([FromForm] SetZaradidRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, errorCode = "ValidationError", message = "Chybí požadované údaje." });

        // Per-action redesign 2026-04-23: vyzvy.pnf.assign. Projekt odvozený z externího
        // odkazu (request.ExterniOdkazId má ZaznamId → ProjektId). Service vrstva v F3
        // validuje specifický per-project check.
        var projektId = await _vyzvaService.ResolveExterniOdkazProjektIdAsync(request.ExterniOdkazId, ct);
        if (projektId is null) return NotFound();
        if (!CurrentUserContext.HasPermission(PermissionKeys.VyzvyPnfAssign, projektId.Value)) return Forbid();

        var result = await _vyzvaService.NastavitZaradidAsync(
            request.ExterniOdkazId, request.Zaradit, ct);

        return result switch
        {
            VyzvaResult<Unit>.Ok => Ok(new { success = true }),
            VyzvaResult<Unit>.Fail f => Ok(new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message }),
            _ => StatusCode(500),
        };
    }

    [HttpPost("prerdit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Prerdit([FromForm] PrerditRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, errorCode = "ValidationError", message = "Chybí požadované údaje." });

        // Per-action redesign 2026-04-23: vyzvy.pnf.reassign.
        var projektId = await _vyzvaService.ResolveExterniOdkazProjektIdAsync(request.ExterniOdkazId, ct);
        if (projektId is null) return NotFound();
        if (!CurrentUserContext.HasPermission(PermissionKeys.VyzvyPnfReassign, projektId.Value)) return Forbid();

        var result = await _vyzvaService.PrerditPnfAsync(
            request.ExterniOdkazId, request.CilovaVyzvaId, ct);

        return result switch
        {
            VyzvaResult<Unit>.Ok => Ok(new { success = true }),
            VyzvaResult<Unit>.Fail f => Ok(new { success = false, errorCode = f.Error.Code.ToString(), message = f.Error.Message }),
            _ => StatusCode(500),
        };
    }

    [HttpGet("reassign-modal")]
    public async Task<IActionResult> ReassignModal(int projektId, CancellationToken ct)
    {
        // Per-action redesign 2026-04-23: vyzvy.pnf.reassign (GET modalu).
        if (!CurrentUserContext.HasPermission(PermissionKeys.VyzvyPnfReassign, projektId)) return Forbid();

        var buffer = await _vyzvaService.GetBufferAsync(projektId, ct);
        var vyzvy = await _vyzvaService.GetVyzvyAsync(projektId, ct);

        var model = new Models.ViewModels.Vyzvy.ReassignModalViewModel
        {
            ProjektId = projektId,
            BufferPolozky = buffer,
            PripravaVyzvy = vyzvy.Where(v => v.Stav == VyzvaStav.Priprava).ToArray(),
        };

        return PartialView("~/Views/Vyzvy/ReassignModal.cshtml", model);
    }
}
