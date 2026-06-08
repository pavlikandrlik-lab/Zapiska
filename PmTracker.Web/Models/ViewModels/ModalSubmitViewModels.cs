namespace PmTracker.Web.Models.ViewModels;

public sealed class ModalSubmitResultViewModel
{
    public bool Ok { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public string? TraceId { get; init; }
    /// <summary>
    /// FIX 2026-05-04: Plný diagnostický log (BuildDiagnosticLog server-side) propsaný klientovi.
    /// JS (modules/ajax.js) ho zobrazí v &lt;details&gt; "Diagnostický log" včetně tlačítek
    /// "Kopírovat" + "Uložit log chyby". Obsahuje TimestampUtc, ErrorCode, TraceId, Request,
    /// Message, FieldErrors, Exception (se SqlException Number/State/Errors a DbUpdateException Entries).
    /// </summary>
    public string? DiagnosticLog { get; init; }
    public Dictionary<string, string[]> FieldErrors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? RefreshScope { get; init; }
    public string? RefreshUrl { get; init; }
    public int? ProjectId { get; init; }
    public int? RecordId { get; init; }
    public int? MeetingId { get; init; }
    public string? UiContext { get; init; }
    public string? Tab { get; init; }
    public string? CiselnikKey { get; init; }
}
