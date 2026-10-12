using System.Net;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Task 1 (2026-10-12): JSON endpoint pro ověření dostupnosti návrhu změny harmonogramu
/// (hlavní cesta — dialog po kliknutí, Task 2) a záložní cesta v NavrhyController.CreateScheduleProposal
/// pro přímý odkaz / druhou kartu / souběh (uživatel 2026-10-08: nikdy chybová stránka).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ScheduleProposalAvailabilityEndpointTests
{
    private readonly ApiSqlFixture _fixture;
    public ScheduleProposalAvailabilityEndpointTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int projectId, int subsystemId, int leadId)> SeedProjectAsync(string marker)
    {
        var leadId = await _fixture.EnsurePersonAsync($"{marker}Lead");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, leadId);
        return (projectId, subsystemId, leadId);
    }

    [Fact]
    public async Task Dostupnost_SCekajicimNavrhem_VraciJsonSDuvodem()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APAVAIL1");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Avail1");
        var proposalId = await _fixture.EnsurePendingScheduleProposalAsync(projectId, subsystemId, recordId, leadId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ScheduleProposalAvailability?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("canCreate").GetBoolean().Should().BeFalse();
        json.GetProperty("title").GetString().Should().Be(ScheduleProposalAvailability.PendingTitle);
        json.GetProperty("message").GetString().Should().Be(ScheduleProposalAvailability.PendingMessage);
        json.GetProperty("proposalUrl").GetString().Should().Contain($"proposalId={proposalId}");
    }

    [Fact]
    public async Task Dostupnost_BezNavrhu_LzeZalozit()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APAVAIL2");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Avail2");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/ScheduleProposalAvailability?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("canCreate").GetBoolean().Should().BeTrue();
        json.GetProperty("title").GetString().Should().BeNull();
        json.GetProperty("proposalUrl").GetString().Should().BeNull();
    }

    [Fact]
    public async Task OtevreniEditoru_SCekajicimNavrhem_VratiZpetSHlaskou_NeChybovouStranku()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APAVAIL3");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Avail3");
        await _fixture.EnsurePendingScheduleProposalAsync(projectId, subsystemId, recordId, leadId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().Contain($"/Projekty/Detail/{projectId}").And.Contain("tab=zaznamy").And.Contain($"recordId={recordId}");

        var page = await client.GetAsync(location.Contains("asUser=") ? location : $"{location}&asUser={_fixture.AdminOsobaId}");
        (await page.Content.ReadAsStringAsync()).Should().Contain("<gov-message color=\"error\">",
            "hláška z TempData se ukáže nahoře na stránce (text kóduje Razor — ověřuje se na modelu)");
    }

    [Fact]
    public async Task OtevreniEditoru_SCekajicimNavrhem_ARetunUrl_VratiNaReturnUrl()
    {
        var (projectId, subsystemId, leadId) = await SeedProjectAsync("APAVAIL4");
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "Avail4");
        await _fixture.EnsurePendingScheduleProposalAsync(projectId, subsystemId, recordId, leadId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var returnUrl = $"/Zaznamy/Detail/{recordId}";
        var response = await client.GetAsync(
            $"/Navrhy/CreateScheduleProposal?projektId={projectId}&zaznamId={recordId}&returnUrl={Uri.EscapeDataString(returnUrl)}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith(returnUrl);
    }
}
