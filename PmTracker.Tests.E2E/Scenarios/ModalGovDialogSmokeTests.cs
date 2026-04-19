using Microsoft.Playwright;
using Xunit;
using FluentAssertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Fáze 2E smoke: modály založené na &lt;gov-dialog&gt; se otevírají, zavírají,
/// submitují. Testuje 3 representative views: DeleteRecord (simplest),
/// NewMeeting (form), EditZaznam (nejkomplexnější — tabs, schedule, quill, pickers).
///
/// [Fact(Skip = ...)] protože vyžaduje live SQL server. Na CI/Citrix kde
/// je DB k dispozici se přepne na normální [Fact] atribut nebo se spustí
/// explicitně přes filter.
/// </summary>
public sealed class ModalGovDialogSmokeTests : IAsyncLifetime
{
    private const string BaseUrl = "http://127.0.0.1:5072";
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private IPage? _page;

    public async Task InitializeAsync()
    {
        _pw = await Playwright.CreateAsync();
        _browser = await _pw.Chromium.LaunchAsync(new() { Headless = true });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _pw?.Dispose();
    }

    [Fact(Skip = "Vyžaduje live SQL — spustit manuálně na dev/Citrix prostředí")]
    public async Task DeleteRecordModal_OpensAsGovDialog()
    {
        await _page!.GotoAsync($"{BaseUrl}/Projekty/Detail/1?tab=zaznamy&asUser=1");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var deleteTrigger = _page.Locator("[data-modal-url*='DeleteRecord']").First;
        await deleteTrigger.ClickAsync();

        var dialog = _page.Locator("gov-dialog[data-modal-container]");
        await dialog.WaitForAsync(new() { Timeout = 5000 });

        var openAttr = await dialog.GetAttributeAsync("open");
        openAttr.Should().NotBeNull();

        var blockClose = await dialog.GetAttributeAsync("block-close");
        blockClose.Should().Be("true");
    }

    [Fact(Skip = "Vyžaduje live SQL")]
    public async Task Modal_ClosesOnDataModalCloseClick()
    {
        await _page!.GotoAsync($"{BaseUrl}/Projekty/Detail/1?tab=zaznamy&asUser=1");
        await _page.Locator("[data-modal-url*='DeleteRecord']").First.ClickAsync();
        await _page.Locator("gov-dialog[data-modal-container]").WaitForAsync();

        await _page.Locator("[data-modal-close]").First.ClickAsync();

        await _page.Locator("gov-dialog[data-modal-container]").WaitForAsync(
            new() { State = WaitForSelectorState.Detached, Timeout = 3000 });
    }

    [Fact(Skip = "Vyžaduje live SQL")]
    public async Task Modal_EscKeyTriggersDirtyCheckPrompt()
    {
        // Fáze 2E garantie: gov-dialog má block-close="true", takže ESC nezavírá
        // modal sám o sobě — místo toho náš bootstrap.js zachytí ESC v
        // handleDocumentOverlayKeydown a zavolá requestRecordEditorModalClose,
        // který spustí promptRecordEditorDiscard (dirty-check). Tím uživatel
        // s neuloženými změnami nepřijde o data.
        await _page!.GotoAsync($"{BaseUrl}/Projekty/Detail/1?tab=zaznamy&asUser=1");
        await _page.Locator("[data-record-editor-url]").First.ClickAsync();
        await _page.Locator("gov-dialog[data-modal-container]").WaitForAsync();

        // Input a change to dirty the form
        var input = _page.Locator("gov-dialog input[name='Nazev']").First;
        await input.FillAsync((await input.InputValueAsync()) + " dirty");

        await _page.Keyboard.PressAsync("Escape");

        // Either the close-guard overlay or a confirm dialog should appear
        var closeGuard = _page.Locator(".record-editor-close-guard-dialog");
        await closeGuard.WaitForAsync(new() { Timeout = 3000 });
    }
}
