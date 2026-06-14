using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectHarmonogramRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectHarmonogramRenderTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Detail_ShouldRenderInlineProjectHeader_LikeMainLayout()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectHeaderOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMHDR");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBHDR", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API project header record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("project-header-row");
        html.Should().Contain("project-title-inline");
        html.Should().Contain("project-status-inline");
        html.Should().Contain("Zkratka: APIHARMHDR");
    }

    [Fact]
    public async Task Detail_ShouldRenderRecordCardsAndTabFallbackLinks_ServerSide()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectRecordsOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMREC");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBREC", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API records fallback record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("API records fallback record");
        html.Should().Contain("data-record-grouped-list");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=zaznamy&amp;asUser=");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=harmonogram&amp;asUser=");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=jednani&amp;asUser=");
        html.Should().Contain($"href=\"/Projekty/Detail/{projectId}?tab=tym&amp;asUser=");
    }

    [Fact]
    public async Task Detail_ShouldDefaultOnlyActiveTasksFilter_ToCheckedState()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectActiveFilterOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMACT");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBACT", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API active filter record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-filter-key=\"aktivni\"");
        html.Should().Contain("checked=\"checked\" data-filter-key=\"aktivni\"");
    }

    [Fact]
    public async Task Detail_ShouldRenderTeamTabSearch_AndSortableTables()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiProjectTeamTabOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMTYM");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBTYM", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API team tab record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=tym&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-tab-panel=\"tym\"");
        html.Should().Contain("data-table-tools-root");
        html.Should().Contain("data-table-tools-search-input");
        html.Should().Contain("data-table-tools-table");
        html.Should().Contain("data-table-sort-button");
        html.Should().Contain("Žádná projektová role neodpovídá zadanému filtru.");
    }

    [Fact]
    public async Task Detail_ShouldRenderCompactOverview_WithSeparateRows_AndAxisBelow()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmonogramOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARM1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUB1", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram layered record");

        // Datum-model: plán pro všech 10 kroků, skutečnost na kroku 1 (= vyplněný segment).
        await SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("schedule-overview-timeline");
        html.Should().Contain(">Plán<");
        html.Should().Contain(">Skutečnost<");
        html.Should().Contain("schedule-overview-track");
        html.Should().Contain("schedule-overview-axis");
        html.Should().Contain("data-timeline-axis");
        html.Should().Contain("data-axis-start=");
        html.Should().Contain("data-axis-end=");
        html.Should().Contain("schedule-overview-marker today");
        html.Should().Contain("schedule-overview-marker deadline");
        html.Should().NotContain("Legenda: Plán / Skutečnost / Termín úkolu");
        html.Should().NotContain("schedule-layered-track schedule-layered-track--overview");

        var overviewRowsBeforeAxis = Regex.IsMatch(
            html,
            "schedule-overview-row[\\s\\S]*schedule-overview-row[\\s\\S]*schedule-overview-axis",
            RegexOptions.CultureInvariant);

        overviewRowsBeforeAxis.Should().BeTrue("compact overview má mít dvě řádky Plán/Skutečnost a osu až pod nimi");
    }

    [Fact]
    public async Task Detail_ShouldRenderBreakdown_WithOwnAxis_AndTodayMarker()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmonogramBreakdownOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARM2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUB2", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram breakdown record");

        // Datum-model: plán pro všech 10 kroků, skutečnost na krocích 1–2.
        await SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-30), new HashSet<int> { 1, 2 });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("schedule-layered-axis");
        html.Should().Contain("data-timeline-axis");
        html.Should().Contain("data-axis-start=");
        html.Should().Contain("data-axis-end=");
        html.Should().Contain("schedule-layered-track schedule-layered-track--step");
        html.Should().Contain("schedule-layered-marker today");
        html.Should().NotContain("schedule-layered-marker deadline");
    }

    [Fact]
    public async Task Detail_ShouldSkipZeroDurationSteps_InCompactOverview()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmonogramZeroDurationOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARMZERO");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUBZERO", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram zero duration record");

        // Datum-model: skutečnost vyplněna na krocích 1 a 3, krok 2 BEZ skutečnosti →
        // ve skutečnost-tracku se absorbuje (nevykreslí vlastní segment).
        const int firstStepOrder = 1;
        const int secondStepOrder = 2;
        const int thirdStepOrder = 3;
        await SeedDatumScheduleAsync(recordId, DateTime.UtcNow.AddDays(-40), new HashSet<int> { firstStepOrder, thirdStepOrder });

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Datum-model: skutečnost vyplněna na krocích 1 a 3, krok 2 bez skutečnosti → absorbován.
        html.Should().Contain($"data-rainbow-segment-label-short=\"{firstStepOrder}\"");
        html.Should().Contain($"data-rainbow-segment-label-short=\"{thirdStepOrder}\"");
        html.Should().NotContain($"data-rainbow-segment-label-short=\"{secondStepOrder}\"");
    }

    /// <summary>
    /// Datum-model seed: vloží krok rows (plan_datum + volitelně skutecnost_datum) do
    /// <c>zaznam_harmonogram_krok</c>. Plán je vždy vyplněn pro všech 10 kroků (týdenní rozestup
    /// od <paramref name="startDate"/>), skutečnost jen pro kroky uvedené v <paramref name="actualSteps"/>.
    /// </summary>
    private async Task SeedDatumScheduleAsync(
        int recordId,
        DateTime startDate,
        IReadOnlySet<int> actualSteps)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var now = DateTime.UtcNow;
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            var plan = startDate.Date.AddDays(poradi * 7);
            DateTime? actual = actualSteps.Contains(poradi) ? plan.AddDays(2) : null;
            dbContext.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = recordId,
                Poradi = (byte)poradi,
                PlanDatum = plan,
                SkutecnostDatum = actual,
                SkutecnostZdroj = actual.HasValue ? (byte)2 : (byte)0,
                SkutecnostRezim = 2,
                UpdatedAt = now
            });
        }
        await dbContext.SaveChangesAsync();
    }
}
