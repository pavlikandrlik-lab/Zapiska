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

    // DRY helpers
    private static string TokensCssPath =>
        Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "tokens.css");

    private static string TokensCssContent() => File.ReadAllText(TokensCssPath);

    [Fact]
    public void TokensCss_Existuje()
    {
        File.Exists(TokensCssPath).Should().BeTrue();
    }

    [Fact]
    public void TokensCss_ObsahujePmColorAliasy()
    {
        var content = TokensCssContent();
        content.Should().Contain("--pm-color-primary");
        content.Should().Contain("var(--color-primary-600");
    }

    [Fact]
    public void TokensCss_ObsahujeSpacingAliasy()
    {
        var content = TokensCssContent();
        content.Should().Contain("--pm-spacing-s");
        content.Should().Contain("--pm-spacing-m");
        content.Should().Contain("--pm-spacing-l");
    }

    [Fact]
    public void TokensCss_ObsahujeBreakpointy()
    {
        var content = TokensCssContent();
        content.Should().Contain("--pm-bp-sm");
        content.Should().Contain("--pm-bp-md");
        content.Should().Contain("--pm-bp-lg");
        content.Should().Contain("--pm-bp-xl");
        content.Should().Contain("--pm-bp-2xl");
    }

    [Fact]
    public void TokensCss_ObsahujeZIndexStack()
    {
        var content = TokensCssContent();
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

    [Fact]
    public void TokensCss_NeobsahujeNeexistujiciGovPrefix()
    {
        // gov-design-system 4.2.7 NEpoužívá prefix --gov-*.
        // Regresní test: kdyby někdo v budoucnu přidal --gov-* odkaz, neresolvoval by na gov tokens.
        var content = TokensCssContent();
        content.Should().NotContain("var(--gov-",
            "gov-design-system nepoužívá --gov- prefix; odkaz by resolvoval pouze fallback");
    }

    [Fact]
    public void TokensCss_ReferencovanyGovTokenExistuje()
    {
        // Ověř, že aspoň jeden token referencovaný v tokens.css skutečně existuje
        // v gov-design-system styles. Kdyby někdo přepsal token bez ověření v gov DS,
        // tento test to odchytí.
        var pmContent = TokensCssContent();
        var govTokensPath = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "lib", "gov-design-system", "styles", "lib", "tokens.min.css");
        var govContent = File.ReadAllText(govTokensPath);

        // Vezmi jeden reprezentativní token z pm (--color-primary-600) a ověř, že je v gov
        pmContent.Should().Contain("var(--color-primary-600", "pm-color-primary odkazuje na --color-primary-600");
        govContent.Should().Contain("--color-primary-600:", "--color-primary-600 musí existovat v gov DS");
    }
}
