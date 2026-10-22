using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R2–R4: detaily záznamu (karta i stránka záznamu) jsou v tabulce se dvěma
/// dvojicemi popisek | hodnota na řádek: Vlastník | Subsystém, Termín | Typ úkolu, Založeno.
/// Typ úkolu jen u záznamu s typem. Na stránce záznamu není samostatný řádek „Založeno“.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordFactsTableRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordFactsTableRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int RecordId)> SeedAsync(string marker, bool withTaskType)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", marker);
        await _fixture.SetRecordFoundingDateAsync(recordId, new DateTime(2026, 7, 8));
        if (withTaskType)
        {
            await using var db = _fixture.CreateDbContext();
            var record = await db.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
            record.AktualniTypUkoluId = await db.CiselnikTypuUkolu.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
            await db.SaveChangesAsync();
        }
        return (projectId, recordId);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    private static string Facts(string html)
        => Regex.Match(html, "<table class=\"record-facts\">[\\s\\S]*?</table>").Value;

    private static IReadOnlyList<string> Labels(string row)
        => Regex.Matches(row, "<th scope=\"row\">([^<]+)</th>").Select(m => m.Groups[1].Value).ToList();

    [Fact]
    public async Task CardDetail_TwoPairsPerRow_WithFoundingDate()
    {
        var s = await SeedAsync("APFACTS1", withTaskType: true);

        var facts = Facts(await GetHtmlAsync(
            $"/Zaznamy/RecordDetailPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}"));

        facts.Should().NotBeEmpty("detaily jsou v tabulce (R2)");
        var rows = Regex.Matches(facts, "<tr>[\\s\\S]*?</tr>").Select(m => m.Value).ToList();
        rows.Should().HaveCount(3);
        Labels(rows[0]).Should().Equal("Vlastník", "Subsystém");
        Labels(rows[1]).Should().Equal("Termín", "Typ úkolu");
        Labels(rows[2]).Should().Equal("Založeno");
        rows[2].Should().Contain("08.07.2026");
    }

    [Fact]
    public async Task CardDetail_WithoutTaskType_HasNoTaskTypeCells()
    {
        var s = await SeedAsync("APFACTS2", withTaskType: false);

        var facts = Facts(await GetHtmlAsync(
            $"/Zaznamy/RecordDetailPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}"));

        facts.Should().NotBeEmpty();
        facts.Should().NotContain("Typ úkolu", "prázdné buňky se nevykreslí (spec 4.B)");
    }

    [Fact]
    public async Task RecordPage_FoundingDateOnlyInTable()
    {
        var s = await SeedAsync("APFACTS3", withTaskType: false);

        var html = await GetHtmlAsync($"/Zaznamy/Detail/{s.RecordId}?asUser={_fixture.AdminOsobaId}");

        var header = Regex.Match(html, "<section class=\"card record-page-header\">[\\s\\S]*?</section>").Value;
        header.Should().Contain("<table class=\"record-facts\">");
        header.Should().NotContain("record-summary-meta", "samostatný řádek Založeno se přesunul do tabulky (R3)");
        Regex.Matches(header, "Založeno").Count.Should().Be(1);
    }
}
