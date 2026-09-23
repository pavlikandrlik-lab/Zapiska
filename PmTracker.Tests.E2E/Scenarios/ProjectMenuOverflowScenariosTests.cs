using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Projektové menu (Option 2, 2026-07-07): 3 primární + sekundární skupina. „+" jednosměrně
/// rozbalí neaktivní sekundární INLINE (ne popover, bez outside-close), zámeček = trvalé
/// rozbalení (reload). Aktivní sekundární tab je vždy inline (podtržení viditelné i sbalené).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ProjectMenuOverflowScenariosTests
{
    private readonly E2ETestFixture _fixture;
    public ProjectMenuOverflowScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private string DetailUrl(string? tab = null)
        => $"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?asUser={_fixture.AdminOsobaId}{(tab is null ? "" : $"&tab={tab}")}";

    private static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    [Fact]
    public async Task PlusExpandsInline_StaysOpen_AndLockSwitchesToPermanent()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(DetailUrl());

        // Sbaleno: neaktivní sekundární „Osoby (tým)" je skryté, „+" viditelný.
        await Expect(page.Locator(".tabs [data-tab='tym']")).ToBeHiddenAsync();
        await Expect(page.Locator("[data-project-menu-toggle]")).ToBeVisibleAsync();

        // „+" rozbalí neaktivní sekundární INLINE v liště tabů (ne v popoveru).
        await page.Locator("[data-project-menu-toggle]").ClickAsync();
        await Expect(page.Locator(".tabs [data-tab='tym']")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-project-menu-lock-toggle]")).ToBeVisibleAsync();

        // Klik mimo NEsbalí (jednosměrné rozbalení).
        await page.Locator("#main").ClickAsync(new() { Position = new() { X = 10, Y = 10 } });
        await Expect(page.Locator(".tabs [data-tab='tym']")).ToBeVisibleAsync();

        // Zámek → reload → trvale rozbaleno (is-expanded), „+" pryč. Zámek spouští
        // window.location.reload() (projectMenu.js); DS gov 4.7.0 má oproti 4.2.9 5×
        // víc <link>/<script> na /assets/gov/ (10 vs. 2) — cachují se natrvalo
        // (StaticAssetCachePolicy.Immutable), ale i tak je celkový reflow/parse na
        // reload nad výchozí 5s expect-timeout. WaitForLoadStateAsync počká na
        // dokončení navigace (výchozí navigation timeout 30s) předtím, než se testuje
        // stav, který renderuje až server po reloadu — bez toho test na zatíženém
        // běhu (celá sada) zvládal doběhnout na hraně a příležitostně selhal.
        await page.Locator("[data-project-menu-lock-toggle]").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.Load);
        await Expect(page.Locator("[data-project-menu-toggle]")).ToHaveCountAsync(0);
        await Expect(page.Locator(".tabs [data-tab='tym']")).ToBeVisibleAsync();

        // Zpět odemknout → „+" zase je, sekundární sbalené.
        await page.Locator("[data-project-menu-lock-toggle]").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.Load);
        await Expect(page.Locator("[data-project-menu-toggle]")).ToBeVisibleAsync();
        await Expect(page.Locator(".tabs [data-tab='tym']")).ToBeHiddenAsync();

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ActiveSecondaryTab_IsInlineWithUnderline_EvenWhenCollapsed()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync(DetailUrl("navrhy"));

        // Na sekundárním tabu (Návrhy) je aktivní tab inline a označený .active i bez rozbalení.
        var navrhy = page.Locator(".tabs [data-tab='navrhy']");
        await Expect(navrhy).ToBeVisibleAsync();
        await Expect(navrhy).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bactive\b"));
        // „+" pořád nabízí zbytek, neaktivní „Osoby (tým)" zůstává skryté.
        await Expect(page.Locator(".tabs [data-tab='tym']")).ToBeHiddenAsync();

        await page.Context.CloseAsync();
    }
}
