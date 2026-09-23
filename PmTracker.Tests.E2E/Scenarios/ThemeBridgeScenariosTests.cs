using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Bez uložené volby platí motiv systému pro celou stránku; přepnutí se uloží do roční
/// cookie pmtracker.theme.mode (spec 2026-09-23 §12.4).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ThemeBridgeScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public ThemeBridgeScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BezVolby_MotivPodleSystemu_APrepnutiSeUlozi()
    {
        var page = await _fixture.NewPageAsync();
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        (await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')"))
            .Should().Be("dark", "bez cookie platí motiv systému — i pro site.css, ne jen gov tokeny");

        await page.WaitForFunctionAsync(
            "document.querySelector('header gov-theme-switch')?.classList.contains('hydrated') === true");
        await page.Locator("header gov-theme-switch button").ClickAsync();
        await page.WaitForFunctionAsync("document.documentElement.getAttribute('data-theme') === 'light'");

        var cookies = await page.Context.CookiesAsync();
        cookies.Should().Contain(c => c.Name == "pmtracker.theme.mode" && c.Value == "light");

        await page.Context.CloseAsync();
    }
}
