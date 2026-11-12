using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Karta grafu (spec 2026-10-15 §12): ECharts kontejner + tabulkový pohled na stejná data;
/// graf bez dat ukáže prázdný stav místo prázdných os. Kotvení na atributy.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectDashboardChartTableRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectDashboardChartTableRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> Report(int projectId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/projekty/{projectId}/dashboard/statistics-panel?rok={DateTime.Today.Year}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task ChartTable_renderuje_kategorie_a_serie()
    {
        const string marker = "APIDASHCHART1";
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var ownerId = await _fixture.EnsurePersonAsync(marker + "OWN");
        var subsystemId = await _fixture.EnsureSubsystemAsync(marker + "S", ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", marker + " ukol");

        var html = await Report(projectId);

        html.Should().Contain("data-chart-key=\"record-status-breakdown\"");
        html.Should().Contain("data-echart").And.Contain("data-chart-json=");
        html.Should().Contain("data-chart-table=\"record-status-breakdown\"");
        html.Should().Contain("data-chart-table-row=\"0\"");
        html.Should().Contain("class=\"pd-data\"");
    }

    [Fact]
    public async Task Graf_bez_dat_ma_prazdny_stav_misto_grafu()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHCHARTEMPTY");

        var html = await Report(projectId);

        html.Should().Contain("data-chart-empty");
        html.Should().NotContain("data-chart-json=", "projekt bez záznamů nemá žádný graf s daty");
    }
}
