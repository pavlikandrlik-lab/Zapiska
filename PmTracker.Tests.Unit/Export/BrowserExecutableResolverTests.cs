using FluentAssertions;
using PmTracker.Web.Services.Export;
using Xunit;

namespace PmTracker.Tests.Unit.Export;

/// <summary>Serverové PDF (2026-09-04): hledání prohlížeče na serveru.</summary>
public sealed class BrowserExecutableResolverTests
{
    [Fact]
    public void Resolve_PrefersConfiguredPath_OverAutodetection()
    {
        var options = new PdfExportOptions { BrowserExecutablePath = @"D:\edge\msedge.exe" };
        var resolver = new BrowserExecutableResolver(options, _ => true);

        resolver.Resolve().Should().Be(@"D:\edge\msedge.exe",
            "nastavená cesta je rozhodnutí správce serveru a má přednost");
    }

    [Fact]
    public void Resolve_KeepsConfiguredPath_EvenWhenMissing()
    {
        var options = new PdfExportOptions { BrowserExecutablePath = @"D:\edge\msedge.exe" };
        var resolver = new BrowserExecutableResolver(options, _ => false);

        resolver.Resolve().Should().Be(@"D:\edge\msedge.exe",
            "tiché sklouznutí na jiný prohlížeč by správci zamlčelo překlep v konfiguraci");
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNoBrowserExists()
    {
        var resolver = new BrowserExecutableResolver(new PdfExportOptions(), _ => false);

        resolver.Resolve().Should().BeNull(
            "bez prohlížeče se tisk vrací na HTML cestu, nesmí spadnout");
    }

    [Fact]
    public void Resolve_FindsFirstExistingCandidate()
    {
        var probed = new List<string>();
        var resolver = new BrowserExecutableResolver(new PdfExportOptions(), path =>
        {
            probed.Add(path);
            return path.Contains("Edge", StringComparison.OrdinalIgnoreCase);
        });

        resolver.Resolve().Should().NotBeNull();
        resolver.Resolve()!.Should().Contain("Edge");
        probed.Should().NotBeEmpty("autodetekce má projít známé cesty");
    }
}
