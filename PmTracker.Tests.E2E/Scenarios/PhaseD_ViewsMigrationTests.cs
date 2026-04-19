using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// E2E smoke testy pro Fázi 2D (migrace Views na pm-* wrappery).
///
/// Ověřuje, že po migraci ~100 výskytů .btn CSS tříd na &lt;pm-button&gt; a
/// 1× &lt;input type="search"&gt; na &lt;pm-search&gt; se stránky renderují bez
/// legacy .btn tlačítek a používají gov-* Web Components.
///
/// Viz docs/superpowers/plans/2026-04-19-faze-2d-migrace-views.md.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class PhaseD_ViewsMigrationTests
{
    private readonly E2ETestFixture _fixture;

    public PhaseD_ViewsMigrationTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dashboard_RenderujeGovButtony_NeLegacyBtnClass()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/");
        response!.Status.Should().Be(200);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Dashboard widgety (News/Meetings/Focus) obsahují „Zobrazit více" link tlačítka
        var govButtonCount = await page.Locator("gov-button").CountAsync();
        govButtonCount.Should().BeGreaterThan(0,
            "Po Fázi 2D mají Dashboard widgety obsahovat alespoň jedno gov-button (z pm-button wrapperu)");

        // V Dashboard views nesmí zůstat legacy .btn třída (mimo layout a modaly)
        var legacyBtnInMain = await page.Locator("main .btn").CountAsync();
        legacyBtnInMain.Should().Be(0,
            "Po Fázi 2D nesmí být v <main> žádné button s class=\"btn\" " +
            "(výjimky _Layout a _ModalFormActions submit jsou mimo <main>)");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task SearchIndex_RenderujeGovFormSearch_PoMigracinaPmSearch()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/Search");
        response!.Status.Should().Be(200);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Search/Index.cshtml byl migrován na <pm-search submit="true"> → <gov-form-search>
        // s vnořeným <gov-form-input slot="input"> a <gov-button slot="button">
        var pageSearchFormInMain = page.Locator("main gov-form-search");
        (await pageSearchFormInMain.CountAsync()).Should().Be(1,
            "Search/Index má mít přesně jedno gov-form-search (z pm-search) ve <main>");

        var searchInput = pageSearchFormInMain.Locator("gov-form-input[slot=\"input\"]");
        (await searchInput.CountAsync()).Should().Be(1,
            "pm-search vnitřně emituje gov-form-input slot=\"input\"");

        var submitButton = pageSearchFormInMain.Locator("gov-button[slot=\"button\"]");
        (await submitButton.CountAsync()).Should().Be(1,
            "pm-search submit=\"true\" emituje gov-button slot=\"button\" jako submit");

        // Po migraci nesmí zůstat native <input type="search"> v <main> na Search/Index
        var nativeSearchInput = await page.Locator("main input[type=\"search\"]").CountAsync();
        nativeSearchInput.Should().Be(0,
            "Search/Index byl migrován — žádný native <input type=\"search\"> v <main>");

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task ProjektyIndex_BackLink_AdAkcniTlacitka_JsouGovButton()
    {
        var page = await _fixture.NewPageAsync();

        var response = await page.GotoAsync($"{_fixture.BaseUrl}/Projekty");
        response!.Status.Should().Be(200);

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // _PageHeader po migraci používá pm-button pro back link (Ghost Small href)
        // Projekty/Index má tlačítko "Nový projekt" (Primary, data-modal-url)
        var allGovButtons = await page.Locator("gov-button").CountAsync();
        allGovButtons.Should().BeGreaterThan(0,
            "Projekty/Index po Fázi 2D musí obsahovat gov-button (z pm-button wrapperů)");

        // "Nový projekt" tlačítko má zachovaný data-modal-url
        var novyProjektButton = page.Locator("gov-button[data-modal-url*=\"NewProjectModal\"]");
        (await novyProjektButton.CountAsync()).Should().Be(1,
            "Nový projekt tlačítko zachovalo data-modal-url atribut po migraci na pm-button");

        await page.Context.CloseAsync();
    }
}
