using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Záznamy tab řadí karty primárně dle kategorie (Informace → Rozhodnutí → Úkol), sekundárně dle
/// čísla. Seed do jednoho subsystému → globální server pořadí = within-subsystem pořadí.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordOrderingRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public RecordOrderingRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RecordsTab_OrdersByCategoryThenNumber()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiOrderOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIORDER");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIORDER_SUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        // Kategorie: U=Úkol, INFO=Informace, ROZHODNUTI=Rozhodnutí (seed fixture).
        var ukol = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Ukol A");
        var info = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "INFO", "Info B");
        var rozh = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "ROZHODNUTI", "Rozhodnuti C");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var order = Regex.Matches(html, "data-record-id=\"(\\d+)\"")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(id => id == ukol || id == info || id == rozh)
            .Distinct()
            .ToArray();

        order.Should().Equal(new[] { info, rozh, ukol },
            "pořadí je kategorie primárně: Informace → Rozhodnutí → Úkol");
    }
}
