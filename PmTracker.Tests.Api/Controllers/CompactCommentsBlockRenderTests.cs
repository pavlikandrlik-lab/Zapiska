using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22: vyjádření v jednom bloku, autor a datum jen v bublině štítku jednání,
/// Upravit / Smazat jen jako ikony řádku a nikdy u uzavřeného jednání (R8).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class CompactCommentsBlockRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public CompactCommentsBlockRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int PmId, int HostId, int RecordId, int OpenCommentId, int ClosedCommentId);

    private async Task<Scenario> SeedAsync(string marker, int meetingBase)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm", "Petr", "Manazer");
        var hostId = await _fixture.EnsurePersonAsync($"{marker}Host");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, hostId, ProjectRoleCodes.Host);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var openMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingBase);
        var closedMeetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", meetingBase + 1);
        await _fixture.AddCommentAsync(recordId, openMeetingId, pmId, $"{marker} otevrene");
        await _fixture.AddCommentAsync(recordId, closedMeetingId, pmId, $"{marker} uzavrene");
        await using var db = _fixture.CreateDbContext();
        var ids = await db.Vyjadreni.Where(x => x.ZaznamId == recordId)
            .Select(x => new { x.Id, x.JednaniId }).ToListAsync();
        return new Scenario(projectId, pmId, hostId, recordId,
            ids.Single(x => x.JednaniId == openMeetingId).Id,
            ids.Single(x => x.JednaniId == closedMeetingId).Id);
    }

    private async Task<string> PanelHtmlAsync(Scenario s, int asUser)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={s.ProjectId}&zaznamId={s.RecordId}&asUser={asUser}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    /// <summary>Markup jednoho řádku: od jeho data-comment-item po další data-comment-item / konec seznamu.</summary>
    private static string RowHtml(string html, int commentId)
    {
        var match = Regex.Match(html,
            $@"data-comment-item[^>]*data-comment-id=""{commentId}"".*?(?=data-comment-item|</div>\s*</div>\s*(?:<div class=""comment-pagination|<form class=""comment-form|$))",
            RegexOptions.Singleline);
        match.Success.Should().BeTrue($"řádek vyjádření {commentId} je v panelu");
        return match.Value;
    }

    [Fact]
    public async Task Panel_IsOneBlock_WithoutPermanentAuthorLine()
    {
        var s = await SeedAsync("ApiCmpBlk", 9621);

        var html = await PanelHtmlAsync(s, s.PmId);

        Regex.Matches(html, "data-comment-list").Count.Should().Be(1);
        Regex.Matches(html, "data-comment-item").Count.Should().Be(2);
        html.Should().NotContain("comment-meta-outer", "autor a datum už nejsou trvale nad vyjádřením");
        html.Should().NotContain("data-comment-wrapper");
    }

    [Fact]
    public async Task EveryComment_HasFocusableMeetingChip_WithAuthorAndDateTooltip()
    {
        var s = await SeedAsync("ApiCmpTip", 9623);
        await using var db = _fixture.CreateDbContext();
        var dates = await db.Vyjadreni.AsNoTracking().Where(x => x.ZaznamId == s.RecordId)
            .ToDictionaryAsync(x => x.Id, x => x.DatumVyjadreni.ToString("dd.MM.yyyy"));

        var html = await PanelHtmlAsync(s, s.HostId);

        foreach (var commentId in new[] { s.OpenCommentId, s.ClosedCommentId })
        {
            var row = RowHtml(html, commentId);
            row.Should().MatchRegex($@"class=""comment-meeting-chip""[^>]*tabindex=""0""[^>]*aria-describedby=""vyjadreni-meta-{commentId}""");
            row.Should().MatchRegex($@"id=""vyjadreni-meta-{commentId}""[^>]*class=""chip-tooltip chip-tooltip--end""[^>]*role=""tooltip""");
            row.Should().Contain("Manazer", "bublina nese autora");
            row.Should().Contain(dates[commentId], "bublina nese datum zápisu");
        }
    }

    [Fact]
    public async Task ProjectManager_RowActionsOnlyOnOpenMeetingComment()
    {
        var s = await SeedAsync("ApiCmpAct", 9625);

        var html = await PanelHtmlAsync(s, s.PmId);

        Regex.Matches(html, "data-comment-row-actions").Count.Should().Be(1);
        RowHtml(html, s.OpenCommentId).Should().Contain("data-comment-row-actions");
        RowHtml(html, s.OpenCommentId).Should().Contain("data-comment-edit-toggle");
        RowHtml(html, s.ClosedCommentId).Should().NotContain("data-comment-row-actions", "u uzavřeného jednání žádné ikony (R8)");
        RowHtml(html, s.ClosedCommentId).Should().NotContain("/Zaznamy/DeleteComment");
    }

    [Fact]
    public async Task Host_NoRowActions_ButTooltips()
    {
        var s = await SeedAsync("ApiCmpHst", 9627);

        var html = await PanelHtmlAsync(s, s.HostId);

        html.Should().NotContain("data-comment-row-actions");
        Regex.Matches(html, @"role=""tooltip""[^>]*data-comment-meta|data-comment-meta[^>]*role=""tooltip""").Count.Should().Be(2);
    }
}
