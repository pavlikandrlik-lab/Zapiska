using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.ExterniOdkaz;
using PmTracker.Web.Services.Security;
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
    private readonly IPmAuthorizationService _authz;
    private readonly ICurrentUserAccessor _currentUser;

    public ExterniOdkazController(
        ITicketingQueryService ticketing,
        IPmAuthorizationService authz,
        ICurrentUserAccessor currentUser)
    {
        _ticketing = ticketing;
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
            return BadRequest(new { Error = "Číslo tiketu musí být přesně 6 cifer." });
        }

        var osobaId = _currentUser.OsobaId;
        if (osobaId is null)
        {
            return Forbid();
        }
        if (!await _authz.HasPermissionAsync(osobaId.Value, PermissionKeys.RecordsEdit, projektId, null, ct))
        {
            return Forbid();
        }

        var dto = await _ticketing.GetZaznamAsync(cislo, ct);
        if (dto is null)
        {
            return Ok(new ExterniOdkazSyncResponse(
                Nalezeno: false, Cislo: cislo, Typ: null, Strucne: null));
        }

        return Ok(new ExterniOdkazSyncResponse(
            Nalezeno: true,
            Cislo: cislo,
            Typ: dto.TypZaznamu,
            Strucne: dto.Strucne));
    }
}
