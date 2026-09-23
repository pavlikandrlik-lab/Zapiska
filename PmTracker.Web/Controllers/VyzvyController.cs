using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;
using PmTracker.Web.Services.Vyzvy.Contracts;

namespace PmTracker.Web.Controllers;

[Authorize]
[Route("vyzvy")]
public sealed class VyzvyController : BaseController
{
    private readonly IVyzvaService _vyzvaService;

    public VyzvyController(
        IUserContextResolver userContextResolver,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        IVyzvaService vyzvaService)
        : base(userContextResolver, timeProvider, loggerFactory)
    {
        _vyzvaService = vyzvaService;
    }

    public sealed class ZaloztRequest
    {
        public int ProjektId { get; set; }
        /// <summary>Pořadové číslo výzvy v roce — zadává uživatel ručně (spec 2026-09-07 §5.1).</summary>
        public int PoradoveVRoce { get; set; }
    }
    public sealed class ZmenitStavRequest { public int VyzvaId { get; set; } public string NovyStav { get; set; } = string.Empty; }
    public sealed class SetZaradidRequest { public int ExterniOdkazId { get; set; } public bool Zaradit { get; set; } }
    public sealed class PrerditRequest { public int ExterniOdkazId { get; set; } public int? CilovaVyzvaId { get; set; } }

    /// <summary>
    /// Formulář Nová výzva. Číslo se domlouvá se SVA mimo aplikaci, proto ho uživatel zadává
    /// ručně; modal jen ukáže, která čísla jsou v daném roce a rámcové smlouvě už obsazená.
    /// </summary>
    [HttpGet("nova-vyzva-modal")]
    public async Task<IActionResult> NovaVyzvaModal(int projektId, int? rok, CancellationToken ct = default)
    {
        if (!CurrentUserContext.HasPermission(PermissionKeys.VyzvyCreate, projektId)) return Forbid();

        // Výzva vždy vzniká v aktuálním roce — rok z railu je jen pohled do historie,
        // do minulého roku se zpětně zakládat nedá. Čas bereme z GetLocalNow() stejně
        // jako Zalozit, jinak by modal na přelomu roku sliboval jiné číslo, než jaké
        // by reálně vzniklo.
        var aktualniRok = GetLocalNow().Year;
        var model = new Models.ViewModels.Vyzvy.NovaVyzvaModalViewModel
        {
            ProjektId = projektId,
            Rok = aktualniRok,
            ProhlizenyRok = rok,
            CisloRamcoveSmlouvy = await _vyzvaService.GetCisloRamcoveSmlouvyAsync(projektId, ct),
            ObsazenaCisla = await _vyzvaService.GetObsazenaCislaAsync(projektId, aktualniRok, ct),
        };

        return View("~/Views/Vyzvy/NovaVyzvaModal.cshtml", model);
    }

    /// <summary>
    /// Založení výzvy. Na rozdíl od ostatních akcí panelu jede z modalu standardní
    /// ajax-submit cestou (data-ajax-submit), proto odpovídá kontraktem
    /// ModalSubmitResultViewModel — jen tak se chyba čísla vykreslí u pole ve formuláři.
    /// </summary>
    [HttpPost("zalozit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Zalozit([FromForm] ZaloztRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return AjaxInvalidModelResult();

        // Per-action redesign 2026-04-23: vyzvy.create (dříve jen CanAccessProject).
        if (!CurrentUserContext.HasPermission(PermissionKeys.VyzvyCreate, request.ProjektId))
            return AjaxForbiddenResult("Nemáte oprávnění zakládat výzvy v tomto projektu.");

        // Rok výzvy je kalendářní údaj, ne časové razítko — musí vyjít z lokálního času.
        // V UTC+1 je 1. ledna po půlnoci v UTC pořád loňský rok a výzva by spadla do
        // loňské číselné řady domluvené se SVA. GetLocalNow() je konvence celé aplikace.
        var now = GetLocalNow();
        var result = await _vyzvaService.ZalozitVyzvuAsync(
            request.ProjektId, request.PoradoveVRoce, CurrentUserContext.OsobaId, now, ct);

        return result switch
        {
            // uiContext nese klíč dlaždice — panel po reloadu skočí rovnou na novou výzvu,
            // protože ji uživatel jde vzápětí plnit (spec §5.3). refreshUrl nese rok:
            // uživatel mohl prohlížet starší rok a nová výzva v jeho railu není.
            VyzvaResult<VyzvaDetail>.Ok ok => AjaxSuccessResult(
                refreshScope: "vyzvy-panel",
                refreshUrl: Url.Action("VyzvyTabPartial", "Projekty",
                    new { id = request.ProjektId, rok = ok.Value.Rok }),
                projectId: request.ProjektId,
                uiContext: $"vyzva-{ok.Value.Id}",
                message: $"Výzva {ok.Value.Kod} založena."),
            VyzvaResult<VyzvaDetail>.Fail f => AjaxErrorResult(
                f.Error.Message,
                AjaxErrorCodes.RecordValidationFailed,
                fieldErrors: JeChybaCisla(f.Error.Code)
                    ? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                        { [nameof(ZaloztRequest.PoradoveVRoce)] = new[] { f.Error.Message } }
                    : null,
                details: f.Error.Code.ToString()),
            _ => StatusCode(500),
        };
    }

    private static bool JeChybaCisla(VyzvaErrorCode code)
        => code is VyzvaErrorCode.InvalidVyzvaNumber or VyzvaErrorCode.DuplicateVyzvaNumber;

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

        var now = GetLocalNow();
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

}
