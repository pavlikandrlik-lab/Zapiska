using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Pokrývá požadavek: modal úpravy záznamu musí jít zavřít křížkem
/// (aria-label „Zavřít dialog") i tlačítkem Zrušit v action bar.
/// Po zavření musí .modal-overlay zmizet z DOM.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordEditModalCloseScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordEditModalCloseScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordEditModal_ShouldCloseViaCloseButton()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        var editButton = page.Locator("button[data-record-editor-url]").First;
        if (await editButton.CountAsync() == 0)
        {
            // Projekt nemá žádný editovatelný záznam — test předčasně ukončit.
            await page.Context.CloseAsync();
            return;
        }

        await editButton.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal).ToBeVisibleAsync();
        await Expect(modal.Locator("form[data-record-editor-form='true']")).ToBeVisibleAsync();

        // Kliknutí na křížek (modal-close s aria-label)
        await modal.GetByRole(AriaRole.Button, new() { Name = "Zavřít dialog" }).ClickAsync();

        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task RecordEditModal_ShouldCloseViaCancelButton()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        var editButton = page.Locator("button[data-record-editor-url]").First;
        if (await editButton.CountAsync() == 0)
        {
            await page.Context.CloseAsync();
            return;
        }

        await editButton.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal).ToBeVisibleAsync();

        // Action bar obsahuje tlačítko "Zrušit" (data-modal-close)
        var cancelButton = modal.Locator("[data-modal-close]").Filter(new() { HasTextString = "Zrušit" });
        if (await cancelButton.CountAsync() == 0)
        {
            // Fallback: jakékoli data-modal-close tlačítko
            cancelButton = modal.Locator("[data-modal-close]").First;
        }

        await cancelButton.ClickAsync();

        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task RecordEditModal_ShouldCloseViaEscapeKey()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        var editButton = page.Locator("button[data-record-editor-url]").First;
        if (await editButton.CountAsync() == 0)
        {
            await page.Context.CloseAsync();
            return;
        }

        await editButton.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("Escape");

        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    /// <summary>
    /// Reprodukce bugu: po přepnutí na záložku Harmonogram v editoru záznamu
    /// přestane modal reagovat na zavírání (křížek ani Escape nefungují), protože
    /// JS v schedule planneru přepíše hodnoty UiHarmonogramDatumy[*] a form se
    /// stane "dirty" → close guard dialog blokuje zavření.
    /// Test běží na úkolovém záznamu (záložka harmonogram v projektu).
    /// </summary>
    [Fact]
    public async Task RecordEditModal_ShouldCloseViaCloseButton_AfterSwitchingToHarmonogramTab()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            // Projekt nemá úkolový záznam se scheduledem; test přeskočíme.
            await page.Context.CloseAsync();
            return;
        }

        await page.Locator(".schedule-card .btn.small", new() { HasTextString = "Upravit" }).First.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal.Locator("form[data-record-editor-form='true']")).ToBeVisibleAsync();

        // Přepnout na záložku Harmonogram
        await modal.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();
        await Expect(modal.Locator("[data-record-modal-panel=\"schedule\"].active")).ToBeVisibleAsync();

        // Kliknout na křížek → modal se musí zavřít bez close-guardu
        await modal.GetByRole(AriaRole.Button, new() { Name = "Zavřít dialog" }).ClickAsync();

        // Pokud se objeví close guard, máme bug — uživatel ho nečekal
        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(0);
        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task RecordEditModal_ShouldCloseViaEscape_AfterSwitchingToHarmonogramTab()
    {
        var page = await _fixture.NewPageAsync();

        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        await page.EvaluateAsync("() => localStorage.setItem('pmtracker.recordEditor.preference', 'modal')");
        await page.ReloadAsync();

        if (await page.Locator(".schedule-card").CountAsync() == 0)
        {
            await page.Context.CloseAsync();
            return;
        }

        await page.Locator(".schedule-card .btn.small", new() { HasTextString = "Upravit" }).First.ClickAsync();

        var modal = page.Locator(".modal-overlay");
        await Expect(modal.Locator("form[data-record-editor-form='true']")).ToBeVisibleAsync();

        await modal.GetByRole(AriaRole.Button, new() { Name = "Harmonogram" }).ClickAsync();
        await Expect(modal.Locator("[data-record-modal-panel=\"schedule\"].active")).ToBeVisibleAsync();

        await page.Keyboard.PressAsync("Escape");

        await Expect(page.Locator("[data-record-editor-close-guard]")).ToHaveCountAsync(0);
        await Expect(page.Locator(".modal-overlay")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator)
    {
        return Assertions.Expect(locator);
    }
}
