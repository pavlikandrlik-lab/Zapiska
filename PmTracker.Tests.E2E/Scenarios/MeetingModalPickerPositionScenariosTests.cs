using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Modal „Nové jednání" je gov-dialog (shadow DOM + centrovací transform). Floating pickery
/// (datum/čas) se musí otevřít TĚSNĚ POD svým polem — ne flipnuté k hornímu okraji obrazovky
/// (degenerovaný host rect gov-dialogu, výška 0) a ne posunuté o transform dialogu.
/// Regrese pro fix 2026-07-07 v ui/floating.js (viewport-boundary fallback + kompenzace
/// transformovaného containing-blocku).
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class MeetingModalPickerPositionScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public MeetingModalPickerPositionScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task NewMeetingModal_DateAndTimePickers_OpenSnugBelowTheirFields()
    {
        var page = await _fixture.NewPageAsync();
        // Vysoký viewport → obě pole (datum i čas) mají dost místa pod sebou (reálný scénář uživatele),
        // takže se pickery mají otevřít POD polem (ne legitimní flip nahoru u krátkého viewportu).
        await page.SetViewportSizeAsync(1280, 1000);
        await page.GotoAsync($"{_fixture.BaseUrl}/Projekty/Detail/{_fixture.ProjectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");

        await page.Locator("[data-modal-url*='NewMeetingModal']").First.ClickAsync();
        await page.WaitForSelectorAsync("form[data-meeting-number-unique='true']");
        await page.WaitForTimeoutAsync(500);

        // ---- DATE ----
        await page.Locator("[data-app-date-field] [data-app-date-open]").First.ClickAsync();
        await page.WaitForTimeoutAsync(300);
        var date = await MeasureAsync(page, "[data-app-date-field]", "[data-app-date-panel]");
        date.visible.Should().BeTrue("datum panel se má otevřít");
        date.deltaTop.Should().BeInRange(0, 40, "datum picker je těsně pod polem (ne flipnutý nahoru)");
        date.deltaLeftAbs.Should().BeLessThanOrEqualTo(24, "datum picker je zarovnaný na levý okraj pole (ne posunutý transformem dialogu)");

        // ---- TIME ---- (otevření času samo zavře datum; NEpoužívat Escape — zavře gov-dialog)
        await page.Locator("[data-app-time-field] [data-app-time-open]").First.ClickAsync();
        await page.WaitForTimeoutAsync(300);
        var time = await MeasureAsync(page, "[data-app-time-field]", "[data-app-time-panel]");
        time.visible.Should().BeTrue("čas panel se má otevřít");
        time.deltaTop.Should().BeInRange(0, 40, "čas picker je těsně pod polem");
        time.deltaLeftAbs.Should().BeLessThanOrEqualTo(24, "čas picker je zarovnaný na levý okraj pole");

        await page.Context.CloseAsync();
    }

    private sealed record Measurement(bool visible, double deltaTop, double deltaLeftAbs);

    private static async Task<Measurement> MeasureAsync(IPage page, string fieldSelector, string panelSelector)
    {
        var json = await page.EvaluateAsync<string>(
            @"(sel) => {
                const field = document.querySelector(sel.f);
                const panel = document.querySelector(sel.p);
                if (!field || !panel || panel.hidden) return JSON.stringify({ visible: false, deltaTop: 0, deltaLeftAbs: 0 });
                const f = field.getBoundingClientRect();
                const p = panel.getBoundingClientRect();
                return JSON.stringify({ visible: true, deltaTop: p.top - f.bottom, deltaLeftAbs: Math.abs(p.left - f.left) });
            }",
            new { f = fieldSelector, p = panelSelector });
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var r = doc.RootElement;
        return new Measurement(r.GetProperty("visible").GetBoolean(), r.GetProperty("deltaTop").GetDouble(), r.GetProperty("deltaLeftAbs").GetDouble());
    }
}
