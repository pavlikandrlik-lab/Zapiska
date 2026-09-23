using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>Toggle Harmonogram na kartě záznamu (2026-07-13): endpoint harmonogramu
/// jednoho záznamu — gov-tag + serverové ticky + ROZBALENÝ rozpad; 404 bez hodnot;
/// regrese: záložka Harmonogram má rozpad dál skrytý.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordScheduleTogglePartialTests
{
    private readonly ApiSqlFixture _fixture;
    public RecordScheduleTogglePartialTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RecordSchedulePartial_RendersTagTicks_AndExpandedBreakdown()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecSchedOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS1", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record schedule partial");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordSchedulePartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("gov-tag", "štítek Stíháme/Nestíháme");
        html.Should().Contain("data-schedule-ticks", "statická osa vyžaduje serverové ticky");
        html.Should().Contain("gantt-steps schedule-steps");
        Regex.IsMatch(html, "class=\"gantt-steps schedule-steps\"[^>]*hidden")
            .Should().BeFalse("na kartě je rozpad rovnou rozbalený (spec R2)");
    }

    [Fact]
    public async Task RecordSchedulePartial_ReturnsNotFound_WithoutScheduleValues()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecSchedEmptyOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS2", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record schedule empty");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordSchedulePartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "záznam bez vyplněné hodnoty kroku nemá harmonogram dlaždici");
    }

    [Fact]
    public async Task ScheduleTab_KeepsBreakdownHidden_Regression()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecSchedTabOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS3", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record schedule tab regression");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        Regex.IsMatch(html, "class=\"gantt-steps schedule-steps\"[^>]*hidden")
            .Should().BeTrue("v záložce Harmonogram se rozpad dál rozbaluje tlačítkem Rozpad");
    }

    [Fact]
    public async Task RecordCard_WithSchedule_RendersSwitchMenuAndShell()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecCardSwitchOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED4");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS4", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API card with switch");
        await _fixture.SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCardPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-record-view-switch");
        html.Should().Contain("data-record-menu-trigger");
        html.Should().Contain($"data-goto-schedule=\"{recordId}\"");
        html.Should().Contain("Navrhnout změnu harmonogramu", "admin má proposals.schedule.create");
        html.Should().Contain("data-record-schedule-url");
        html.Should().Contain("record-expand-indicator");
        html.Should().NotContain("record-toggle-indicator");
    }

    [Fact]
    public async Task RecordCard_WithoutScheduleValues_HasNoSwitchNorGotoSchedule()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecCardNoSchedOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECSCHED5");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIRECSCHEDS5", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API card no schedule");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCardPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-record-view-switch", "bez harmonogramu není co přepínat (R6)");
        html.Should().NotContain("data-goto-schedule", "překlik by vedl na neexistující dlaždici");
        html.Should().NotContain("data-record-schedule-shell");
        // Úkol s právem návrhu má menu s jedinou položkou (návrh změny harmonogramu).
        html.Should().Contain("data-record-menu-trigger");
        html.Should().Contain("Navrhnout změnu harmonogramu");
    }

    [Fact]
    public async Task RecordCard_Menu_LinksToRecordDetailPage()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiRecPageLinkOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIRECPAGELINK");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiRecPageLinkSub", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API record page link");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCardPartial?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("Otevřít na nové kartě");
        html.Should().Contain($"/Zaznamy/Detail/{recordId}");
    }
}
