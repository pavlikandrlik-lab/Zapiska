using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Shell dashboardu projektu (spec 2026-10-15 §8): systémové pm-tabs se třemi záložkami a souhrnný pás
/// načítaný z endpointu <c>souhrn</c>. Kotvení na atributy a ASCII (Razor kóduje diakritiku).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectDashboardShellRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectDashboardShellRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Index_renderuje_pm_tabs_se_tremi_klici()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSHELL");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/projekty/{projectId}/dashboard?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("<pm-tabs").And.Contain($"persist-key=\"dashboard-{projectId}\"").And.Contain("persist=\"storage\"");
        html.Should().Contain("key=\"zaznamy\"").And.Contain("key=\"nes\"").And.Contain("key=\"statistiky\"");
        html.Should().Contain($"data-url=\"/projekty/{projectId}/dashboard/souhrn\"");
        html.Should().NotContain("class=\"dashboard-tab", "vlastní tlačítka záložek nahradily systémové pm-tabs");
    }

    [Fact]
    public async Task Index_s_dashTab_predava_vynucenou_zalozku()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSHELLTAB");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync($"/projekty/{projectId}/dashboard?dashTab=nes&asUser={_fixture.AdminOsobaId}");

        html.Should().Contain("data-dashboard-force-tab=\"nes\"");
    }

    [Fact]
    public async Task Souhrn_endpoint_vraci_dlazdice_s_data_souhrn_tab()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSOUHRN");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/projekty/{projectId}/dashboard/souhrn?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-souhrn-tab=\"zaznamy\"").And.Contain("data-souhrn-filter=\"delayed\"");
        html.Should().Contain("data-souhrn-klic=\"ceka-na-skutecnost\"");
        // Ticketing je v Api testech vypnutý → projekt bez napojení nemá ticketové dlaždice.
        html.Should().NotContain("data-souhrn-tab=\"nes\"");
    }

    [Fact]
    public async Task Souhrn_endpoint_bez_pristupu_k_projektu_403()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDASHSOUHRN403");
        var cizi = await _fixture.EnsurePersonAsync("APIDASHSOUHRNCIZI");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/projekty/{projectId}/dashboard/souhrn?asUser={cizi}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
