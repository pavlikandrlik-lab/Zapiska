using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Services.Export;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using Xunit;

namespace PmTracker.Tests.Integration.Export;

/// <summary>
/// Serverové PDF (2026-09-04): skutečné vygenerování dokumentu headless prohlížečem.
/// Databázi nepotřebuje, proto stojí mimo SqlIntegrationCollection.
/// </summary>
public sealed class ChromiumPdfRendererTests
{
    private const string ThreePageHtml = """
        <html><head><meta charset="utf-8"></head><body>
        <div class="p">Strana jedna — příliš žluťoučký kůň úpěl ďábelské ódy</div>
        <div class="p">Strana dvě</div>
        <div>Strana tři</div>
        </body></html>
        """;

    private const string PageBreakCss =
        "body{font-family:Arial;font-size:12pt} .p{page-break-after:always;height:250mm}";

    private static string Squash(string value)
        => new(value.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

    [Fact]
    public async Task RenderAsync_NumbersEveryPage_InCenteredFooter()
    {
        var options = new PdfExportOptions();
        var resolver = new BrowserExecutableResolver(options, File.Exists);

        resolver.Resolve().Should().NotBeNull(
            "test skutečné sazby PDF potřebuje na stroji Edge nebo Chrome; " +
            "tiše přeskočený test by číslování nehlídal");

        var cssPath = Path.Combine(Path.GetTempPath(), $"pmtracker-pdf-test-{Guid.NewGuid():N}.css");
        await File.WriteAllTextAsync(cssPath, PageBreakCss);

        try
        {
            using var renderer = new ChromiumPdfRenderer(
                Options.Create(options), resolver, NullLogger<ChromiumPdfRenderer>.Instance);

            var result = await renderer.RenderAsync(
                new PdfRenderRequest { Html = ThreePageHtml, StylesheetPath = cssPath },
                CancellationToken.None);

            result.Succeeded.Should().BeTrue($"sazba selhala: {result.FailureReason}");

            using var pdf = PdfDocument.Open(result.Bytes!);
            pdf.NumberOfPages.Should().Be(3, "tři bloky s vynuceným zlomem = tři strany");

            for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
            {
                var text = ContentOrderTextExtractor.GetText(pdf.GetPage(pageNumber));
                Squash(text).Should().Contain($"Strana{pageNumber}z3",
                    $"na straně {pageNumber} musí být patička s vlastním číslem");
            }
        }
        finally
        {
            File.Delete(cssPath);
        }
    }

    [Fact]
    public async Task RenderAsync_ReportsFailure_WhenDisabled()
    {
        var options = new PdfExportOptions { Enabled = false };
        using var renderer = new ChromiumPdfRenderer(
            Options.Create(options),
            new BrowserExecutableResolver(options, File.Exists),
            NullLogger<ChromiumPdfRenderer>.Instance);

        var result = await renderer.RenderAsync(
            new PdfRenderRequest { Html = "<html><body>x</body></html>" }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("vypnut", "vypínač musí být v důvodu čitelně vidět");
    }

    [Fact]
    public async Task RenderAsync_ReportsFailure_WhenNoBrowserFound()
    {
        var options = new PdfExportOptions();
        using var renderer = new ChromiumPdfRenderer(
            Options.Create(options),
            new BrowserExecutableResolver(options, _ => false),
            NullLogger<ChromiumPdfRenderer>.Instance);

        var result = await renderer.RenderAsync(
            new PdfRenderRequest { Html = "<html><body>x</body></html>" }, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("prohlížeč");
    }
}
