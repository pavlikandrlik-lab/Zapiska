using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22 C (R6–R12):
/// - detail jednání ukáže jen záznamy jednání;
/// - z menu karty „Otevřít na celou obrazovku“ vede na stránku záznamu se šipkami;
/// - klávesy ← → procházejí záznamy v pořadí zápisu a na krajích nepřetáčí;
/// - v editoru vyjádření klávesy nereagují;
/// - drobečková lišta vrátí do jednání.
/// Vlastní projekt (E2EOwnProject), takže sadu záznamů jednání má test pod kontrolou.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordSequenceScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordSequenceScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    private sealed record Seed(int MeetingId, IReadOnlyList<int> MeetingRecordIds, int OutsideRecordId);

    /// <summary>
    /// Pořadí zápisu: subsystém s pořadím 1 („… B“) → záznamy 1, 3; subsystém s pořadím 2 („… A“) → záznam 2.
    /// Záznam 4 je uzavřený a minulé jednání není → do jednání nepatří.
    /// </summary>
    private async Task<Seed> SeedAsync()
    {
        var project = await E2EOwnProject.CreateAsync(_fixture.Database.ConnectionString, _fixture.AdminOsobaId);
        var r1 = await project.AddTaskAsync(1, project.FirstSubsystemId);
        var r2 = await project.AddTaskAsync(2, project.SecondSubsystemId);
        var r3 = await project.AddTaskAsync(3, project.FirstSubsystemId);
        var r4 = await project.AddTaskAsync(4, project.FirstSubsystemId, closed: true);
        var meetingId = await project.AddOpenMeetingAsync(501, DateTime.Today.AddDays(-3));
        return new Seed(meetingId, [r1, r3, r2], r4);
    }

    /// <summary>
    /// Dev identita: asUser nesou jen drobečky (WithAsUser). Odkazy z menu karty a tlačítka šipek
    /// ho nenesou, proto ho kontext doplní ke každému požadavku na aplikaci (jako ruční průchod).
    /// </summary>
    private async Task<IPage> NewPageAsUserAsync()
    {
        var page = await _fixture.NewPageAsync();
        await page.Context.RouteAsync($"{_fixture.BaseUrl}/**", async route =>
        {
            var url = route.Request.Url;
            if (url.Contains("asUser=", StringComparison.Ordinal))
            {
                await route.ContinueAsync();
                return;
            }
            var separator = url.Contains('?') ? "&" : "?";
            await route.ContinueAsync(new RouteContinueOptions { Url = $"{url}{separator}asUser={_fixture.AdminOsobaId}" });
        });
        return page;
    }

    /// <summary>
    /// Stisk klávesy až po navázání posluchače — pozici vykreslí server dřív, než se načte JS modul
    /// (recordSequenceKeys.js nastaví data-record-sequence-keys-ready).
    /// </summary>
    private static async Task PressWhenReadyAsync(IPage page, string key)
    {
        await Expect(page.Locator("[data-record-sequence-nav]"))
            .ToHaveAttributeAsync("data-record-sequence-keys-ready", "true");
        await page.Keyboard.PressAsync(key);
    }

    /// <summary>
    /// Klávesa stránku neopustí: navigace pryč se do 1,5 s nesmí objevit. (Kontrola URL hned po
    /// stisku by prošla i během rozběhnuté navigace; dvojice ← → by se vrátila na stejný záznam.)
    /// </summary>
    private static async Task PressAndAssertStaysAsync(IPage page, int recordId, string key)
    {
        var leaves = page.WaitForURLAsync(
            url => !url.Contains($"/Zaznamy/Detail/{recordId}?", StringComparison.Ordinal),
            new PageWaitForURLOptions { Timeout = 1500 });
        await PressWhenReadyAsync(page, key);
        await FluentActions.Awaiting(() => leaves).Should().ThrowAsync<TimeoutException>(
            $"{key} nesmí opustit záznam {recordId}");
    }

    [Fact]
    public async Task MeetingDetail_FullScreen_ArrowKeysWalkMeetingRecordsInMinutesOrder_BreadcrumbReturns()
    {
        var seed = await SeedAsync();
        var page = await NewPageAsUserAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Jednani/Detail/{seed.MeetingId}");
            await Expect(page.Locator(".record-card[data-record-id]")).ToHaveCountAsync(3);
            await Expect(page.Locator($".record-card[data-record-id='{seed.OutsideRecordId}']")).ToHaveCountAsync(0);

            var first = page.Locator($".record-card[data-record-id='{seed.MeetingRecordIds[0]}']");
            await first.Locator("[data-record-menu-trigger]").DispatchEventAsync("click");
            // Otevřené menu se přesune do #floating-panel-root (anchoredMenu.js); menu ostatních karet
            // zůstávají skrytá v kartách.
            var openItem = page.Locator("#floating-panel-root [data-record-open-page='same-tab']");
            await Expect(openItem).ToBeVisibleAsync();
            await openItem.ClickAsync();

            await Expect(page).ToHaveURLAsync(new Regex($"/Zaznamy/Detail/{seed.MeetingRecordIds[0]}\\?seznam=jednani-{seed.MeetingId}"));
            var position = page.Locator("[data-record-sequence-position]");
            await Expect(position).ToHaveTextAsync("1 / 3");

            // Kraj seznamu: ← na prvním nic neudělá (nepřetáčí se).
            await PressAndAssertStaysAsync(page, seed.MeetingRecordIds[0], "ArrowLeft");

            for (var index = 1; index < 3; index++)
            {
                await PressWhenReadyAsync(page, "ArrowRight");
                await Expect(page).ToHaveURLAsync(new Regex($"/Zaznamy/Detail/{seed.MeetingRecordIds[index]}\\?"));
                await Expect(position).ToHaveTextAsync($"{index + 1} / 3");
            }

            await PressAndAssertStaysAsync(page, seed.MeetingRecordIds[2], "ArrowRight");

            await PressWhenReadyAsync(page, "ArrowLeft");
            await Expect(position).ToHaveTextAsync("2 / 3");

            await page.Locator(".app-breadcrumb-back").ClickAsync();
            await Expect(page).ToHaveURLAsync(new Regex($"/Jednani/Detail/{seed.MeetingId}"));
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }

    [Fact]
    public async Task ArrowKeys_InCommentEditor_DoNotSwitchRecord()
    {
        var seed = await SeedAsync();
        var page = await NewPageAsUserAsync();
        try
        {
            await page.GotoAsync($"{_fixture.BaseUrl}/Zaznamy/Detail/{seed.MeetingRecordIds[1]}?seznam=jednani-{seed.MeetingId}");
            await Expect(page.Locator("[data-record-sequence-position]")).ToHaveTextAsync("2 / 3");
            var editor = page.Locator("[data-record-comments-shell] .ql-editor").First;
            await editor.ClickAsync();
            await page.Keyboard.TypeAsync("ab");

            await PressAndAssertStaysAsync(page, seed.MeetingRecordIds[1], "ArrowLeft");
            await PressAndAssertStaysAsync(page, seed.MeetingRecordIds[1], "ArrowRight");
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }
}
