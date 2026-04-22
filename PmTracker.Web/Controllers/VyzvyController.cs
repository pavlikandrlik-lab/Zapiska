using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.Entities;
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
        if (!CurrentUserContext.CanAccessProject(request.ProjektId)) return Forbid();

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
        if (!Enum.TryParse<VyzvaStav>(request.NovyStav, ignoreCase: false, out var stav))
            return BadRequest(new { success = false, errorCode = "InvalidStateTransition", message = "Neplatný stav." });

        var vyzva = await _vyzvaService.GetVyzvaAsync(request.VyzvaId, ct);
        if (vyzva == null) return NotFound();
        if (!CurrentUserContext.CanAccessProject(vyzva.ProjektId)) return Forbid();

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
        // ACL: služba sama odmítne non-PNF a uzamčené výzvy.
        // Základní ACL přes CanAccessProject není triviální (vyžadovalo by načíst projektId přes vazbu),
        // plný role check (proj_man/adm_proj) se provádí v UI (VM.MuzeEditovat) a na straně service.
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
        if (!CurrentUserContext.CanAccessProject(projektId)) return Forbid();

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
