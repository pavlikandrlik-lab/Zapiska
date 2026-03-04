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
    public async Task Detail_ShouldRenderCompactOverview_WithSeparateRows_AndAxisBelow()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiHarmonogramOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIHARM1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIHARMSUB1", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API harmonogram layered record");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
                .Where(x => x.Id == recordId)
                .Select(x => new
                {
                    x.Id,
                    x.HarmonogramSablonaVerze
                })
                .FirstAsync();

            var firstStepOrder = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && !x.JeZpozdeni)
                .OrderBy(x => x.KrokPoradi)
                .Select(x => x.KrokPoradi)
                .FirstAsync();

            var durationTypeId = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && !x.JeZpozdeni && x.KrokPoradi == firstStepOrder)
                .Select(x => x.Id)
                .FirstAsync();

            var delayTypeId = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && x.JeZpozdeni && x.KrokPoradi == firstStepOrder)
                .Select(x => x.Id)
                .FirstAsync();

            dbContext.ZaznamHarmonogramHodnoty.AddRange(
            [
                new ZaznamHarmonogramHodnotaEntity
                {
                    ZaznamId = record.Id,
                    TypId = durationTypeId,
                    HodnotaInt = 6
                },
                new ZaznamHarmonogramHodnotaEntity
                {
                    ZaznamId = record.Id,
                    TypId = delayTypeId,
                    HodnotaInt = -2
                }
            ]);

            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("schedule-overview-timeline");
        html.Should().Contain(">Plán<");
        html.Should().Contain(">Skutečnost<");
        html.Should().Contain("schedule-overview-track");
        html.Should().Contain("schedule-overview-axis");
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

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var record = await dbContext.ProjektoveZaznamy.AsNoTracking()
                .Where(x => x.Id == recordId)
                .Select(x => new { x.Id, x.HarmonogramSablonaVerze })
                .FirstAsync();

            var firstStepOrder = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && !x.JeZpozdeni)
                .OrderBy(x => x.KrokPoradi)
                .Select(x => x.KrokPoradi)
                .FirstAsync();

            var durationTypeId = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && !x.JeZpozdeni && x.KrokPoradi == firstStepOrder)
                .Select(x => x.Id)
                .FirstAsync();

            var delayTypeId = await dbContext.CiselnikHarmonogramTypu.AsNoTracking()
                .Where(x => x.SablonaVerze == record.HarmonogramSablonaVerze && x.JeZpozdeni && x.KrokPoradi == firstStepOrder)
                .Select(x => x.Id)
                .FirstAsync();

            dbContext.ZaznamHarmonogramHodnoty.AddRange(
            [
                new ZaznamHarmonogramHodnotaEntity
                {
                    ZaznamId = record.Id,
                    TypId = durationTypeId,
                    HodnotaInt = 6
                },
                new ZaznamHarmonogramHodnotaEntity
                {
                    ZaznamId = record.Id,
                    TypId = delayTypeId,
                    HodnotaInt = 2
                }
            ]);

            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=harmonogram&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("schedule-layered-axis");
        html.Should().Contain("schedule-layered-track schedule-layered-track--step");
        html.Should().Contain("schedule-layered-marker today");
        html.Should().NotContain("schedule-layered-marker deadline");
    }
}
