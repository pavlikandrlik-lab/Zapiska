using FluentAssertions;
using Microsoft.Playwright;
using PmTracker.Tests.E2E.TestInfrastructure;

namespace PmTracker.Tests.E2E.Scenarios;

/// <summary>
/// Spec 2026-10-20 Přehled — nadpisy bloků: kompaktní (≤ 33 px), nad seznamem,
/// seznam dál roluje uvnitř bloku, na úzkém displeji nic nepřetéká.
/// </summary>
[Collection(E2ECollection.CollectionName)]
public sealed class DashboardHeadingsScenariosTests
{
    private readonly E2ETestFixture _fixture;

    public DashboardHeadingsScenariosTests(E2ETestFixture fixture) => _fixture = fixture;

    // Playwright .NET plní návratový typ přes bezparametrický ctor + settery (poziční record neumí).
    private sealed class HeadingBox
    {
        public string Block { get; set; } = "";
        public double Height { get; set; }
        public double Bottom { get; set; }
        public double ContentTop { get; set; }
    }

    [Fact]
    public async Task Widescreen_NadpisyKompaktni_NadObsahem_SeznamRoluje()
    {
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(1400, 900);
        await page.GotoAsync($"{_fixture.BaseUrl}/Dashboard?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("[data-dashboard-panel='focus'] [data-dashboard-panel-content] .dashboard-panel-body"))
            .ToHaveCountAsync(1);

        var boxes = await page.EvaluateAsync<HeadingBox[]>(@"() =>
            ['focus', 'meetings', 'news'].map(block => {
                const section = document.querySelector(`[data-dashboard-panel='${block}']`);
                const h = section.querySelector('h2.dashboard-panel-heading').getBoundingClientRect();
                const c = section.querySelector('[data-dashboard-panel-content]').getBoundingClientRect();
                return { block, height: h.height, bottom: h.bottom, contentTop: c.top };
            })");

        foreach (var box in boxes)
        {
            box.Height.Should().BeLessThanOrEqualTo(33, $"nadpis bloku {box.Block} má zabrat nejvýš jeden kompaktní řádek");
            box.Bottom.Should().BeLessThanOrEqualTo(box.ContentTop + 0.5, $"nadpis bloku {box.Block} je nad obsahem");
        }

        var focusListOverflow = await page.EvaluateAsync<string?>(
            "() => { const l = document.querySelector('.dashboard-focus-list'); return l ? getComputedStyle(l).overflowY : null; }");
        if (focusListOverflow is not null)
        {
            focusListOverflow.Should().BeOneOf(new[] { "auto", "scroll" }, "seznam záznamů dál roluje uvnitř bloku");
        }

        await page.Context.CloseAsync();
    }

    [Fact]
    public async Task UzkyDisplej_NadpisyBezVodorovnehoPretekani()
    {
        var page = await _fixture.NewPageAsync();
        await page.SetViewportSizeAsync(375, 800);
        await page.GotoAsync($"{_fixture.BaseUrl}/Dashboard?asUser={_fixture.AdminOsobaId}");
        await Assertions.Expect(page.Locator("h2.dashboard-panel-heading")).ToHaveCountAsync(3);

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        overflow.Should().BeFalse();

        await page.Context.CloseAsync();
    }
}
