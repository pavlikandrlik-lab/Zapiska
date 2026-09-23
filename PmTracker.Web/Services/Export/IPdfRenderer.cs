namespace PmTracker.Web.Services.Export;

public sealed record PdfRenderRequest
{
    public required string Html { get; init; }

    /// <summary>Absolutní cesta k tiskovému CSS; null = HTML si nese styly samo.</summary>
    public string? StylesheetPath { get; init; }
}

/// <summary>
/// Výsledek sazby. Selhání se nevyhazuje výjimkou — fallback na HTML tisk je
/// řízený tok, ne odchyt výjimky, a jde otestovat bez prohlížeče.
/// </summary>
public sealed record PdfRenderResult(byte[]? Bytes, string? FailureReason)
{
    public bool Succeeded => Bytes is not null;

    public static PdfRenderResult Success(byte[] bytes) => new(bytes, null);

    public static PdfRenderResult Failure(string reason) => new(null, reason);
}

public interface IPdfRenderer
{
    Task<PdfRenderResult> RenderAsync(PdfRenderRequest request, CancellationToken ct);
}
