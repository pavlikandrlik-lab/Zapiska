using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Záložka Záznamy (spec 2026-10-15 §9): filtrační chipy s počty, karta tabulky, odkaz na záznam,
/// prázdný stav. Výpočet kategorií se nemění — testy kotví jen na vzhled a počty (atributy, ASCII).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed partial class ProjectDashboardRecordsPanelRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectDashboardRecordsPanelRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [GeneratedRegex("data-dashboard-category-filter=\"(?<k>[a-z]*)\"[^>]*data-count=\"(?<n>\\d+)\"")]
    private static partial Regex ChipRegex();

    private async Task<(int ProjectId, int DelayedId, string Html)> PanelSDaty(string marker)
    {
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var ownerId = await _fixture.EnsurePersonAsync(marker + "OWN");
        var subsystemId = await _fixture.EnsureSubsystemAsync(marker + "S", ownerId);

        // Krok 1 se skutečností 2 dny po plánu → V prodlení.
        var delayedId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", marker + " prodleni");
        await _fixture.SetRecordFoundingDateAsync(delayedId, DateTime.Today.AddDays(-30));
        await _fixture.SeedDatumScheduleAsync(delayedId, DateTime.Today.AddDays(-30), new HashSet<int> { 1 });

        // Plán kroků 1–2 v minulosti bez skutečnosti → Čeká na skutečnost.
        var awaitingId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", marker + " ceka");
        await _fixture.SetRecordFoundingDateAsync(awaitingId, DateTime.Today.AddDays(-20));
        await _fixture.SeedDatumScheduleAsync(awaitingId, DateTime.Today.AddDays(-20), new HashSet<int>());

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/projekty/{projectId}/dashboard/records-panel?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return (projectId, delayedId, html);
    }

    [Fact]
    public async Task Chipy_maji_pocty()
    {
        var (_, _, html) = await PanelSDaty("APIDASHREC1");

        var chipy = ChipRegex().Matches(html).ToDictionary(m => m.Groups["k"].Value, m => int.Parse(m.Groups["n"].Value));
        chipy.Keys.Should().BeEquivalentTo(["", "delayed", "awaiting", "approaching"]);

        foreach (var kategorie in new[] { "delayed", "awaiting", "approaching" })
        {
            var radku = Regex.Matches(html, $"data-category=\"{kategorie}\"").Count;
            chipy[kategorie].Should().Be(radku, $"chip {kategorie} ukazuje počet řádků své kategorie");
        }
        chipy[""].Should().Be(chipy["delayed"] + chipy["awaiting"] + chipy["approaching"]);
        chipy["delayed"].Should().BeGreaterThan(0);
        chipy["awaiting"].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Nulova_kategorie_disabled()
    {
        var (_, _, html) = await PanelSDaty("APIDASHREC2");

        var chip = Regex.Match(html, "<button[^>]*data-dashboard-category-filter=\"approaching\"[^>]*>");
        chip.Success.Should().BeTrue(html);
        chip.Value.Should().Contain("data-count=\"0\"").And.Contain("disabled");

        var neprazdny = Regex.Match(html, "<button[^>]*data-dashboard-category-filter=\"delayed\"[^>]*>");
        neprazdny.Value.Should().NotContain("disabled", "Razor null atribut vynechá — neprázdný chip je klikací");
    }

    [Fact]
    public async Task Detail_ma_odkaz_na_zaznam()
    {
        var (_, delayedId, html) = await PanelSDaty("APIDASHREC3");

        html.Should().Contain($"href=\"/Zaznamy/Detail/{delayedId}");
        html.Should().Contain("class=\"pd-row pd-row--delayed\"");
        html.Should().Contain("pd-offset pd-offset--delayed");
    }

    [Fact]
    public async Task Prazdny_stav_ma_pd_empty()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHRECEMPTY");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync($"/projekty/{projectId}/dashboard/records-panel?asUser={_fixture.AdminOsobaId}");

        html.Should().Contain("pd-empty").And.Contain("name=\"check-circle-fill\"");
        html.Should().NotContain("<table");
    }
}
