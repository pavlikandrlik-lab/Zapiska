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
    public async Task DarkMode_MaJednoPoleAJednoTlacitkoHledat()
    {
        var page = await _fixture.NewPageAsync();

        var baseUri = new Uri(_fixture.BaseUrl);
        await page.Context.AddCookiesAsync(new[]
        {
            new Cookie { Name = "pmtracker.theme.mode", Value = "dark", Domain = baseUri.Host, Path = "/" }
        });

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");
        (await page.EvaluateAsync<string>("document.documentElement.getAttribute('data-theme')"))
            .Should().Be("dark");

        // Tmavý motiv nesmí ovládací prvky hledání zdvojit. Odeslání je textové tlačítko
        // „Hledat" podle gov designu — ikonu lupy formulář nemá.
        var form = page.Locator("[data-global-search]");
        await Assertions.Expect(form.Locator("input[name='q']")).ToHaveAttributeAsync("role", "combobox");
        await Assertions.Expect(form.Locator("input")).ToHaveCountAsync(1);

        var submit = form.Locator("gov-button[slot='button']");
        await Assertions.Expect(submit).ToHaveCountAsync(1);
        await Assertions.Expect(submit.Locator("button")).ToHaveCountAsync(1);
        await Assertions.Expect(submit).ToContainTextAsync("Hledat");

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

    [Fact]
    public async Task Vyhledavani_PoHydrataci_JeComboboxBezNaseptavaceProhlizece()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        // Vnitřní <input> vykreslí gov-form-input až po hydrataci; global-search.js se na něj
        // musí napojit až potom (dřív skončil na null a dropdown nikdy nefungoval).
        var input = page.Locator("[data-global-search] input[name='q']");
        await Assertions.Expect(input).ToHaveAttributeAsync("role", "combobox");
        await Assertions.Expect(input).ToHaveAttributeAsync("aria-controls", "global-search-listbox");
        await Assertions.Expect(input).ToHaveAttributeAsync("autocomplete", "off");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task Vyhledavani_Krizek_JeVidetJenSDotazem()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty?asUser={_fixture.AdminOsobaId}");

        var input = page.Locator("[data-global-search] input[name='q']");
        await Assertions.Expect(input).ToHaveAttributeAsync("role", "combobox");

        // Host gov-buttonu nemá vlastní box (viz Playwright + gov komponenty) — viditelnost
        // se čte z computed display, ne přes ToBeVisible.
        const string eraseDisplayed =
            "() => getComputedStyle(document.querySelector('[data-global-search-erase]')).display !== 'none'";

        (await page.EvaluateAsync<bool>(eraseDisplayed)).Should().BeFalse("prázdné pole nemá křížek");

        await input.FillAsync("test");
        await page.WaitForFunctionAsync(eraseDisplayed);

        await page.Locator("[data-global-search-erase] button").ClickAsync();
        await Assertions.Expect(input).ToHaveValueAsync("");
        // Hodnotu musí převzít i komponenta, jinak by ji příští vykreslení vrátilo zpět.
        await Assertions.Expect(page.Locator("[data-global-search] gov-form-input")).ToHaveJSPropertyAsync("value", "");
        await page.WaitForFunctionAsync($"() => !({eraseDisplayed})()");
        await Assertions.Expect(page.Locator("[data-global-search-dropdown]")).ToBeHiddenAsync();

        await page.Context.CloseAsync();
    }
}
