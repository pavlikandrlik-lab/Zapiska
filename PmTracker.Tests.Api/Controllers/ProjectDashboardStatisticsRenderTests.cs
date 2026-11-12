using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Záložka Statistiky (spec 2026-10-15 §11, §14): endpoint <c>statistics-panel</c> vrací nový report se sekcemi,
/// výběrem období a srovnáním s předchozím obdobím; samostatný <c>zakladni-report</c> zanikl.
/// Kotvení na atributy a ASCII (Razor kóduje diakritiku).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectDashboardStatisticsRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectDashboardStatisticsRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(HttpStatusCode Status, string Html)> Get(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url + (url.Contains('?') ? "&" : "?") + $"asUser={_fixture.AdminOsobaId}");
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StatisticsPanel_vraci_report_se_sekcemi()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSTAT1");
        var ownerId = await _fixture.EnsurePersonAsync("APIDASHSTAT1OWN");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIDASHSTAT1S", ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "APIDASHSTAT1 ukol");

        var (status, html) = await Get($"/projekty/{projectId}/dashboard/statistics-panel");

        status.Should().Be(HttpStatusCode.OK, html);
        foreach (var sekce in new[] { "prehled", "prubeh", "terminy", "lide", "servicedesk" })
        {
            html.Should().Contain($"data-section=\"{sekce}\"");
        }
        html.Should().Contain("data-chart-key=\"prehled-kpi\"").And.Contain("data-chart-key=\"burn-up\"");
        html.Should().Contain($"data-url=\"/projekty/{projectId}/dashboard/statistics-panel\"");
        html.Should().Contain("data-obdobi-srovnani");
        html.Should().NotContain("dashboard-kpi-", "legacy panel statistik je pryč");
    }

    [Fact]
    public async Task StatisticsPanel_bez_is_ma_v_sekci_servicedesk_odkaz_na_nastaveni()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSTAT2");

        var (_, html) = await Get($"/projekty/{projectId}/dashboard/statistics-panel");

        html.Should().Contain("data-chart-key=\"sd-info\"").And.Contain("data-chart-action").And.Contain("href=\"/Projekty\"");
        html.Should().NotContain("data-chart-key=\"sd-open-by-type\"", "bez napojení se nekreslí prázdné grafy");
    }

    [Fact]
    public async Task StatisticsPanel_kvartal_parametr()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSTAT3");

        var (status, html) = await Get($"/projekty/{projectId}/dashboard/statistics-panel?rok=2025&kvartal=1");

        status.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-obdobi=\"Q1 2025\"");
        html.Should().Contain("<option value=\"1\" selected=\"selected\">");
        html.Should().Contain("data-obdobi-srovnani=\"Q4 2024\"");
    }

    [Fact]
    public async Task StatisticsPanel_neplatny_kvartal_je_400()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSTAT4");

        var (status, _) = await Get($"/projekty/{projectId}/dashboard/statistics-panel?rok=2025&kvartal=7");

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Zakladni_report_endpoint_neexistuje()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSTAT5");

        var (status, _) = await Get($"/projekty/{projectId}/dashboard/zakladni-report?rok=2025");

        status.Should().Be(HttpStatusCode.NotFound);
    }
}
