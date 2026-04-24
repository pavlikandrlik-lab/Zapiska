namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Výsledek pre-save validace nové externí vazby na SD ticket.
/// Plán 3 Feature D (2026-04-24, U10): hard constraint — bez SD fingerprintu
/// nelze novou vazbu založit.
/// </summary>
public sealed record ExterniOdkazValidationResult(bool IsValid, string? ErrorCode, string? ErrorMessage)
{
    public static ExterniOdkazValidationResult Ok() => new(true, null, null);

    public static ExterniOdkazValidationResult Fail(string code, string msg) => new(false, code, msg);

    public const string CodeFormat = "format";
    public const string CodeDisabled = "ticketing_disabled";
    public const string CodeNotFound = "ticket_not_found";
    public const string CodeSdUnavailable = "sd_unavailable";
}
