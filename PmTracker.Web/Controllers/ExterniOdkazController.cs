using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.ExterniOdkaz;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;
using IPmAuthorizationService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Web.Controllers;

/// <summary>
/// Endpoint pro auto-sync externí vazby (Plán B).
/// Po zadání 6místného čísla tiketu UI volá POST /ExterniOdkaz/Sync —
/// server dohledá záznam v intranetNEW ServiceDesku přes
/// <see cref="ITicketingQueryService.GetZaznamAsync"/> a vrátí Typ + Strucné.
/// </summary>
/// <remarks>
/// Autorizace: manuální check <c>records.edit</c> vůči <c>projektId</c>
/// z form body (policy attribute by nefungoval — endpoint nemá projektId
/// v route a records.edit je project-scoped).
/// </remarks>
[Authorize]
[Route("ExterniOdkaz")]
public sealed class ExterniOdkazController : Controller
{
    private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);

    private readonly ITicketingQueryService _ticketing;
    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly IPmAuthorizationService _authz;
    private readonly ICurrentUserAccessor _currentUser;

    public ExterniOdkazController(
        ITicketingQueryService ticketing,
        IVyjadreniQueryService vyjadreni,
        IPmAuthorizationService authz,
        ICurrentUserAccessor currentUser)
    {
        _ticketing = ticketing;
        _vyjadreni = vyjadreni;
        _authz = authz;
        _currentUser = currentUser;
    }

    [HttpPost("Sync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(
        [FromForm] string cislo,
        [FromForm] int projektId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cislo) || !SixDigits.IsMatch(cislo))
        {
            return BadRequest(new { error = "Číslo tiketu musí být přesně 6 cifer." });
        }

        var osobaId = _currentUser.OsobaId;
        if (osobaId is null)
        {
            return Forbid();
        }
        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.ExterniOdkazySync, projektId, null, ct))
        {
            return Forbid();
        }

        var dto = await _ticketing.GetZaznamAsync(cislo, ct);
        if (dto is null)
        {
            return Ok(new ExterniOdkazSyncResponse(
                Nalezeno: false, Cislo: cislo, Typ: null, Strucne: null,
                DatumObjednani: null, PlanDodani: null, DatumDodani: null, DatumPrevzeti: null,
                Vyjadreni: Array.Empty<ExterniOdkazVyjadreniPreviewDto>()));
        }

        // FIX 2026-05-02: auto-harvest payload pro pre-Save buffer flow.
        // Server přečte vyjádření tiketu + spočítá 4 datumy přes shared
        // PerTicketMetadataExtractor (= zrcadlí PerTicketMetadataSyncService.SyncTicketAsync,
        // ale bez DB persistence — výsledek dostane klient do localStorage bufferu).
        var fingerprints = await _vyjadreni.GetHotZaznamFingerprintsAsync(new[] { cislo }, ct);
        DateTime? slaDeadline = null;
        if (fingerprints.TryGetValue(cislo, out var fp))
        {
            slaDeadline = fp.SlaDeadline;
        }

        var vyjadreniList = await _vyjadreni.GetVyjadreniForTicketAsync(cislo, sinceUtc: null, ct);
        var metadata = PerTicketMetadataExtractor.Extract(dto.TypZaznamu, slaDeadline, vyjadreniList);

        var preview = vyjadreniList
            .Select(v => new ExterniOdkazVyjadreniPreviewDto(
                v.Id, v.Datum, v.Typ, v.Zpracoval, v.Popis))
            .ToList();

        return Ok(new ExterniOdkazSyncResponse(
            Nalezeno: true,
            Cislo: cislo,
            Typ: dto.TypZaznamu,
            Strucne: dto.Strucne,
            DatumObjednani: metadata.DatumObjednani,
            PlanDodani: metadata.PlanDodani,
            DatumDodani: metadata.DatumDodani,
            DatumPrevzeti: metadata.DatumPrevzeti,
            Vyjadreni: preview));
    }
}
