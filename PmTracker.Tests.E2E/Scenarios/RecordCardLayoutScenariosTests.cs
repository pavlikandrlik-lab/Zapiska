using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;
using static Microsoft.Playwright.Assertions;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-22 A, B. Hlavička rozbalené karty začíná na stejné svislé čáře jako detaily
/// a vyjádření, protože indikátor rozbalení je v levém okraji (R1). Detaily v tabulce nahradí
/// řádek „Termín · Založeno“ v hlavičce; v pohledu Harmonogram ten řádek zůstane (R2, R3).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class RecordCardLayoutScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public RecordCardLayoutScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    /// <summary>Záložka Záznamy vlastního projektu s odkazem na záznam — karta se sama rozbalí.</summary>
    private async Task<ILocator> OpenExpandedCardAsync(IPage page, int projectId, int recordId)
    {
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{projectId}?tab=zaznamy&recordId={recordId}&asUser={_fixture.AdminOsobaId}");
        var card = page.Locator($".record-card[data-record-id='{recordId}']");
        await Expect(card).ToHaveCountAsync(1);
        await Expect(card).Not.ToHaveClassAsync(new Regex(@"\bcollapsed\b"));
        return card;
    }

    private static Task<double> LeftAsync(ILocator locator)
        => locator.First.EvaluateAsync<double>("e => e.getBoundingClientRect().left");

    [Theory]
    [InlineData(1400)]
    [InlineData(375)]
    public async Task ExpandedCard_HeaderStartsOnSameLineAsBody(int width)
    {
        var project = await E2EOwnProject.CreateAsync(_fixture.Database.ConnectionString, _fixture.AdminOsobaId);
        var recordId = await project.AddTaskAsync(1, project.FirstSubsystemId);
        var page = await _fixture.NewPageAsync();
        try
        {
            await page.SetViewportSizeAsync(width, 900);
            var card = await OpenExpandedCardAsync(page, project.ProjectId, recordId);

            var titleLeft = await LeftAsync(card.Locator(".record-title-wrap"));
            var bodyLeft = await LeftAsync(card.Locator("[data-record-comments-shell]"));
            Math.Abs(titleLeft - bodyLeft).Should().BeLessThanOrEqualTo(1,
                $"hlavička a tělo karty začínají na jedné svislé čáře (R1), šířka {width}");

            // Rotace indikátoru má transition 0,2 s — za běhu je bounding box otáčeného čtverce širší.
            var indicatorLocator = card.Locator(".record-expand-indicator");
            await indicatorLocator.EvaluateAsync("e => Promise.all(e.getAnimations().map(a => a.finished))");
            var indicator = await indicatorLocator.BoundingBoxAsync();
            indicator.Should().NotBeNull();
            ((double)indicator!.X + indicator.Width).Should().BeLessThanOrEqualTo(titleLeft + 1,
                "indikátor je v levém okraji, ne přes text");
        }
        finally
        {
            await page.Context.CloseAsync();
        }
    }
}
