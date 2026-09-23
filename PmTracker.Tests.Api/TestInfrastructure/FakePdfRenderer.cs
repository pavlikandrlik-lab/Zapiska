using PmTracker.Web.Services.Export;

namespace PmTracker.Tests.Api.TestInfrastructure;

/// <summary>
/// Podvržený generátor — Api testy nesmí spouštět prohlížeč. Zachytí, co dostal,
/// a umí na povel selhat, aby šlo ověřit návrat na HTML tisk.
/// Testy sdílí factory (kolekce běží sériově); kdo přepne <see cref="ShouldFail"/>,
/// musí ho ve finally vrátit zpět.
/// </summary>
public sealed class FakePdfRenderer : IPdfRenderer
{
    public static readonly byte[] Payload = "%PDF-1.4 podvržený obsah"u8.ToArray();

    public string? LastHtml { get; private set; }
    public string? LastStylesheetPath { get; private set; }
    public bool ShouldFail { get; set; }

    public Task<PdfRenderResult> RenderAsync(PdfRenderRequest request, CancellationToken ct)
    {
        LastHtml = request.Html;
        LastStylesheetPath = request.StylesheetPath;

        return Task.FromResult(ShouldFail
            ? PdfRenderResult.Failure("test: generátor záměrně selhal")
            : PdfRenderResult.Success(Payload));
    }
}
