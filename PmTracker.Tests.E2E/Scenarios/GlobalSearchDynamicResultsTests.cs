using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

[Collection(E2ECollection.CollectionName)]
public sealed class GlobalSearchDynamicResultsTests
{
    private readonly E2ETestFixture _fixture;

    public GlobalSearchDynamicResultsTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Vyhledavani_PriPsani_ZobraziDropdown()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        var input = page.Locator("input[name='q']");
        await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Napíšeme dostatečně dlouhý dotaz
        await input.FillAsync("test");

        // Čekáme max 2s na zobrazení dropdownu (debounce 300ms + fetch)
        var dropdown = page.Locator("[data-global-search-dropdown]");
        await dropdown.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2000 });

        var isVisible = await dropdown.IsVisibleAsync();
        isVisible.Should().BeTrue("po napsání ≥2 znaků se musí zobrazit dropdown s výsledky");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Vyhledavani_Escape_ZavreDropdown()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        var input = page.Locator("input[name='q']");
        await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await input.FillAsync("test");

        var dropdown = page.Locator("[data-global-search-dropdown]");
        // Čekáme na zobrazení dropdownu
        await dropdown.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2000 });

        // Stiskneme Escape
        await input.PressAsync("Escape");

        // Dropdown se musí skrýt
        await dropdown.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 1000 });
        var isHidden = await dropdown.IsHiddenAsync();
        isHidden.Should().BeTrue("Escape musí zavřít dropdown");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Vyhledavani_KlikMimo_ZavreDropdown()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        var input = page.Locator("input[name='q']");
        await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await input.FillAsync("test");

        var dropdown = page.Locator("[data-global-search-dropdown]");
        await dropdown.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 2000 });

        // Klikneme mimo formulář (na body)
        await page.Locator("body").ClickAsync(new() { Position = new() { X = 10, Y = 10 } });

        await dropdown.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 1000 });
        var isHidden = await dropdown.IsHiddenAsync();
        isHidden.Should().BeTrue("klik mimo formulář musí zavřít dropdown");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task DarkMode_MaJedinouLupu_VDom()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        // Přepneme na dark mode
        var themeSwitch = page.Locator("[data-theme-switch-input]");
        if (await themeSwitch.IsVisibleAsync())
        {
            // Zapneme dark mode
            var isChecked = await themeSwitch.IsCheckedAsync();
            if (!isChecked)
            {
                await themeSwitch.CheckAsync();
                await page.WaitForTimeoutAsync(300);
            }
        }

        // Ověříme že ikona lupy je v DOM právě jednou
        var searchIcons = page.Locator("[data-global-search] .gov-icon--system svg");
        var count = await searchIcons.CountAsync();
        count.Should().Be(1, "v dark mode musí být v search formuláři právě jedna ikona lupy");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Vyhledavani_KratkeDotazy_NezobrazujiDropdown()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        var input = page.Locator("input[name='q']");
        await input.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Napíšeme jen 1 znak
        await input.FillAsync("a");
        await page.WaitForTimeoutAsync(500);

        var dropdown = page.Locator("[data-global-search-dropdown]");
        var isHidden = await dropdown.IsHiddenAsync();
        isHidden.Should().BeTrue("dotaz kratší než 2 znaky nesmí zobrazit dropdown");

        await page.Context.CloseAsync();
    }
}
