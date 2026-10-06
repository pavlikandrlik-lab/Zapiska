using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Lokální odchylky hlavičky od DS (docs/known-issues/ds-fis-odchylky.md č. 6 a 7):
/// nižší hlavní navigace a obsah hlavičky přes celou šířku obrazovky.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class HeaderLayoutScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public HeaderLayoutScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private sealed record Geometry(
        double Viewport, double ContentWidth, double NavListWidth, double NavLinkHeight, double SearchWidth);

    private async Task<Geometry> MeasureAsync(int width)
    {
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(width, 1000);
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("[data-global-search] input[name='q']")).ToHaveAttributeAsync("role", "combobox");

        var m = await page.EvaluateAsync<double[]>(@"() => [
            document.documentElement.clientWidth,
            document.querySelector('.gov-header__content').getBoundingClientRect().width,
            document.querySelector('.app-main-nav > ul').getBoundingClientRect().width,
            document.querySelector('.app-main-nav > ul > li > a').getBoundingClientRect().height,
            document.querySelector('[data-global-search]').getBoundingClientRect().width
        ]");

        await page.Context.CloseAsync();
        return new Geometry(m[0], m[1], m[2], m[3], m[4]);
    }

    [Fact]
    public async Task Widescreen_HlavickaPresCelouSirku_NavigaceNizkaAVSirceStranky()
    {
        var g = await MeasureAsync(1920);

        g.ContentWidth.Should().BeApproximately(g.Viewport, 1, "obsah hlavičky se roztáhne přes celou obrazovku");
        g.NavListWidth.Should().BeLessThan(g.Viewport - 100, "navigace zůstává v šířce stránky DS (75rem)");
        g.NavLinkHeight.Should().BeApproximately(25, 0.5, "položky hlavní navigace jsou nižší než DS 48 px");
        g.SearchWidth.Should().BeGreaterThan(424, "hledání se roztahuje, už nemá pevných 26.5rem")
            .And.BeLessThanOrEqualTo(560);
    }

    [Fact]
    public async Task UzkyDisplej_MobilniMenuDrziDotykoveCile()
    {
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(600, 1000);
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        // Pod 48 em jsou položky v rozbalovacím menu pod hamburgerem — zůstávají na 48 px.
        var height = await page.EvaluateAsync<double>(
            "() => parseFloat(getComputedStyle(document.querySelector('.app-main-nav > ul > li > a')).height)");
        height.Should().BeApproximately(48, 0.5);

        await page.Context.CloseAsync();
    }
}
