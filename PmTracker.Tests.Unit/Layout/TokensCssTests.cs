using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

public sealed class TokensCssTests
{
    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!;
    }

    [Fact]
    public void TokensCss_Existuje()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void TokensCss_ObsahujePmColorAliasy()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-color-primary");
        content.Should().Contain("var(--gov-color-");
    }

    [Fact]
    public void TokensCss_ObsahujeSpacingAliasy()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-spacing-s");
        content.Should().Contain("--pm-spacing-m");
        content.Should().Contain("--pm-spacing-l");
    }

    [Fact]
    public void TokensCss_ObsahujeBreakpointy()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-bp-sm");
        content.Should().Contain("--pm-bp-md");
        content.Should().Contain("--pm-bp-lg");
        content.Should().Contain("--pm-bp-xl");
        content.Should().Contain("--pm-bp-2xl");
    }

    [Fact]
    public void TokensCss_ObsahujeZIndexStack()
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css"));
        content.Should().Contain("--pm-z-header");
        content.Should().Contain("--pm-z-dropdown");
        content.Should().Contain("--pm-z-modal");
    }

    [Fact]
    public void Layout_ImportujeTokensCss()
    {
        var layoutPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "Shared", "_Layout.cshtml");
        var layout = File.ReadAllText(layoutPath);
        layout.Should().Contain("~/css/tokens.css", "tokens.css musí být načteno v layoutu");
    }

    [Fact]
    public void Layout_TokensCss_JePoGovCssAPredSiteCss()
    {
        var layoutPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "Shared", "_Layout.cshtml");
        var layout = File.ReadAllText(layoutPath);
        var tokensIndex = layout.IndexOf("~/css/tokens.css", StringComparison.Ordinal);
        var coreCssIndex = layout.IndexOf("core.min.css", StringComparison.Ordinal);
        var siteCssIndex = layout.IndexOf("~/css/site.css", StringComparison.Ordinal);
        tokensIndex.Should().BeGreaterThan(coreCssIndex, "tokens.css musí být po gov core.min.css (přepisuje gov tokeny)");
        tokensIndex.Should().BeLessThan(siteCssIndex, "tokens.css musí být před site.css (aby ji site.css viděla)");
    }
}
