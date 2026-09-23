using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Bug 2026-07-04: text-selection drag z inputu v modalu ven na ztmavené pozadí
/// (backdrop) a puštění myši tam zavřel celý modal. Click po drag-selectu se
/// retargetuje na společného předka (gov-dialog) a stará backdrop-close větev to
/// vyhodnotila jako kliknutí na pozadí. Modal se má zavřít jen křížkem / Escape /
/// tlačítkem — ne gestem myši končícím na backdropu.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class ModalBackdropDragCloseScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public ModalBackdropDragCloseScenariosTests(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NewMeetingModal_DragSelectReleasedOnBackdrop_ShouldNotClose()
    {
        var page = await _fixture.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");

        var trigger = page.Locator("[data-modal-url*='NewMeeting']").First;
        if (await trigger.CountAsync() == 0)
        {
            // Projekt nemá afordanci „Nové jednání" — test přeskočíme.
            await page.Context.CloseAsync();
            return;
        }

        await trigger.ClickAsync();

        var dialog = page.Locator("gov-dialog[data-modal-container]");
        // Attached (ne default Visible) — gov-dialog host má výšku 0.
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = 5000 });

        // Start dragu = textový input v obsahu modalu.
        var input = dialog.Locator("input:not([type='hidden'])").First;
        await input.WaitForAsync(new() { Timeout = 5000 });
        var box = await input.BoundingBoxAsync();
        box.Should().NotBeNull("modal musí mít viditelný input pro start text-selection dragu");

        // Reprodukce gesta: mousedown na input, drag na levý horní roh viewportu
        // (backdrop mimo vycentrovaný obsah), mouseup tam. Browser vyšle `click`
        // s targetem = gov-dialog (společný předek down/up targetů).
        await page.Mouse.MoveAsync(box!.X + (box.Width / 2), box.Y + (box.Height / 2));
        await page.Mouse.DownAsync();
        await page.Mouse.MoveAsync(5, 5, new() { Steps = 8 });
        await page.Mouse.UpAsync();

        // Modal MUSÍ zůstat otevřený — gesto nesmí zavřít modal.
        // ToHaveCount (ne ToBeVisible): gov-dialog host má výšku 0 (obsah je uvnitř),
        // Playwright ho proto hlásí jako hidden i když je modal otevřený.
        await Assertions.Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(1);

        // Sanity: Escape modal pořád zavírá (regrese guard legitimní close cesty).
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(page.Locator("gov-dialog[data-modal-container]")).ToHaveCountAsync(0);

        await page.Context.CloseAsync();
    }
}
