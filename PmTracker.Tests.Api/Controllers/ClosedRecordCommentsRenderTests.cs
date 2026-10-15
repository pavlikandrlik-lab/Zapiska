using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Záznam ve stavu is_final (2026-10-14): panel vyjádření nenabídne editor ani výběr jednání,
/// jen poznámku. Existující vyjádření se dál zobrazují; u uzavřeného jednání nejdou upravit.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ClosedRecordCommentsRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ClosedRecordCommentsRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int ProjectId, int PmId, int OwnerId, int SubsystemId, int OpenRecordId, int ClosedRecordId, int OpenMeetingId);

    private async Task<Scenario> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, "PROJ_MAN");
        var openRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} otevreny");
        var closedRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} uzavreny");
        await _fixture.CloseRecordAsync(closedRecordId);
        var openMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);
        return new Scenario(projectId, pmId, ownerId, subsystemId, openRecordId, closedRecordId, openMeetingId);
    }

    private async Task<string> CommentsHtmlAsync(int projectId, int recordId, int asUser)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/RecordCommentsPartial?projektId={projectId}&zaznamId={recordId}&asUser={asUser}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Fact]
    public async Task OpenRecord_PmSeesForm_AndNoClosedNote()
    {
        var s = await SeedAsync("ApiCls1", 9981);

        var html = await CommentsHtmlAsync(s.ProjectId, s.OpenRecordId, s.PmId);

        html.Should().Contain("action=\"/Zaznamy/AddComment\"");
        html.Should().Contain($"<option value=\"{s.OpenMeetingId}\"");
        html.Should().NotContain("data-record-closed-note");
    }

    [Fact]
    public async Task ClosedRecord_PmSeesNoFormAndNoMeetingSelect_ButClosedNote()
    {
        var s = await SeedAsync("ApiCls2", 9983);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ClosedRecordId, s.PmId);

        html.Should().NotContain("action=\"/Zaznamy/AddComment\"");
        html.Should().NotContain("name=\"JednaniId\"");
        html.Should().Contain("data-record-closed-note=\"true\"");
    }

    [Fact]
    public async Task ClosedRecord_ExistingCommentStillRenderedAndEditableInOpenMeeting()
    {
        var s = await SeedAsync("ApiCls3", 9985);
        await _fixture.AddCommentAsync(s.ClosedRecordId, s.OpenMeetingId, s.PmId, "ApiCls3 historicke vyjadreni");

        var html = await CommentsHtmlAsync(s.ProjectId, s.ClosedRecordId, s.PmId);

        html.Should().Contain("ApiCls3 historicke vyjadreni");
        html.Should().Contain("data-comment-edit-toggle", "uzavřený záznam nezamyká úpravy; ty řídí stav jednání");
    }

    [Fact]
    public async Task CommentInClosedMeeting_HasNoEditOrDeleteControls()
    {
        var s = await SeedAsync("ApiCls4", 9987);
        await _fixture.AddCommentAsync(s.OpenRecordId, s.OpenMeetingId, s.PmId, "ApiCls4 vyjadreni");
        var closedMeetingId = await _fixture.CreateMeetingAsync(s.ProjectId, "CLOSED", 9988);
        await _fixture.AddCommentAsync(s.OpenRecordId, closedMeetingId, s.PmId, "ApiCls4 uzavrene jednani");

        var html = await CommentsHtmlAsync(s.ProjectId, s.OpenRecordId, s.PmId);

        html.Should().Contain("ApiCls4 uzavrene jednani");
        CountOccurrences(html, "data-comment-edit-toggle").Should().Be(1, "jen vyjádření u otevřeného jednání jde upravit");
        CountOccurrences(html, "action=\"/Zaznamy/DeleteComment\"").Should().Be(1);
    }

    [Fact]
    public async Task ClosedRecord_ViewerWithoutRecordsEdit_SeesNoClosedNote()
    {
        var s = await SeedAsync("ApiCls5", 9989);
        var deputyId = await _fixture.EnsurePersonAsync("ApiCls5Deputy");
        await _fixture.EnsureSubsystemRoleAsync(s.ProjectId, s.SubsystemId, deputyId, SubsystemRoleCodes.DeputyLead);

        var html = await CommentsHtmlAsync(s.ProjectId, s.ClosedRecordId, deputyId);

        html.Should().NotContain("action=\"/Zaznamy/AddComment\"");
        html.Should().NotContain("data-record-closed-note",
            "výzva „znovu otevřete změnou stavu“ nemá smysl pro toho, kdo stav měnit nesmí");
    }

    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
