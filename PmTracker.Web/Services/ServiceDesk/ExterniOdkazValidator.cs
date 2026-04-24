using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PmTracker.ServiceDesk.Contracts;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Hard constraint validator pro vytvoření externí vazby na SD ticket.
/// Pravidlo: <c>cislo</c> musí být přesně 6 cifer (HOT_ZAZNAMY.id je int,
/// user-facing 6-místný), SD integrace musí být zapnutá
/// (<c>Ticketing:Enabled=true</c>) a ticket musí existovat v HOT_ZAZNAMY
/// (ověřeno přes <see cref="IVyjadreniQueryService.GetHotZaznamFingerprintsAsync"/>).
/// </summary>
/// <remarks>
/// Plán 3 Feature D (2026-04-24, U10). Navazuje na memory pravidla:
/// — <c>feedback_sd_ticket_id_required.md</c> (ticket bez id je mimo scope)
/// — <c>project_servicedesk_infosystem_binding.md</c> § „Externí vazba hard constraint".
/// Volá se z <see cref="PmTracker.Web.Services.RecordService"/> PŘED
/// <c>SaveChangesAsync</c> — jen pro NOVĚ přidávané SD vazby. Stávající záznamy
/// migrace nikdy neruší.
/// </remarks>
public sealed class ExterniOdkazValidator
{
    private static readonly Regex SixDigits = new(@"^\d{6}$", RegexOptions.Compiled);

    private readonly IVyjadreniQueryService _vyjadreni;
    private readonly IOptions<TicketingOptions> _options;
    private readonly ILogger<ExterniOdkazValidator> _logger;

    public ExterniOdkazValidator(
        IVyjadreniQueryService vyjadreni,
        IOptions<TicketingOptions> options,
        ILogger<ExterniOdkazValidator> logger)
    {
        _vyjadreni = vyjadreni;
        _options = options;
        _logger = logger;
    }

    public async Task<ExterniOdkazValidationResult> ValidateCreateAsync(string cislo6, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cislo6) || !SixDigits.IsMatch(cislo6))
        {
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeFormat,
                "Číslo ticketu musí být přesně 6 cifer (např. 123456).");
        }

        if (!_options.Value.Enabled)
        {
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeDisabled,
                "SD integrace je vypnutá — novou externí vazbu nelze založit. Kontaktuj administrátora.");
        }

        IReadOnlyDictionary<string, HotZaznamFingerprintDto> fingerprints;
        try
        {
            fingerprints = await _vyjadreni
                .GetHotZaznamFingerprintsAsync(new[] { cislo6 }, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Zrušení requestu nechceme převádět na „SD unavailable" — caller
            // rozlišuje cancellation od skutečného selhání SD.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SD lookup selhal při validaci externí vazby pro #{Cislo}",
                cislo6);
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeSdUnavailable,
                "ServiceDesk není dostupný — nelze ověřit existenci ticketu. Zkus to později.");
        }

        if (!fingerprints.ContainsKey(cislo6))
        {
            return ExterniOdkazValidationResult.Fail(
                ExterniOdkazValidationResult.CodeNotFound,
                $"Ticket #{cislo6} v ServiceDesku neexistuje — externí vazbu nelze založit.");
        }

        return ExterniOdkazValidationResult.Ok();
    }
}
