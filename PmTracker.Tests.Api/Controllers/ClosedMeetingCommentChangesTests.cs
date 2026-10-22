using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-22 R8: vyjádření k uzavřenému jednání nejde upravit ani smazat — ani PM,
/// ani podvrženým požadavkem. UI tlačítka nevykreslí (ClosedRecordCommentsRenderTests),
/// tady se hlídá server (CommentService.EnsureMeetingAllowsCommentChangesAsync).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ClosedMeetingCommentChangesTests
{
    private readonly ApiSqlFixture _fixture;

    public ClosedMeetingCommentChangesTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int PmId, int RecordId, int CommentId)> SeedAsync(string marker, int meetingNumber)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var pmId = await _fixture.EnsurePersonAsync($"{marker}Pm");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, pmId, ProjectRoleCodes.ProjectManager);
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        var closedMeetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", meetingNumber);
        await _fixture.AddCommentAsync(recordId, closedMeetingId, pmId, $"{marker} k uzavrenemu jednani");
        await using var db = _fixture.CreateDbContext();
        var commentId = await db.Vyjadreni.Where(x => x.ZaznamId == recordId).Select(x => x.Id).SingleAsync();
        return (projectId, pmId, recordId, commentId);
    }

    [Fact]
    public async Task UpdateComment_ClosedMeeting_IsRefused_TextUnchanged()
    {
        var s = await SeedAsync("ApiClsMtgUpd", 9611);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/UpdateComment?asUser={s.PmId}",
            ApiTestHttpHelper.BuildForm(
                ("projektId", s.ProjectId.ToString()),
                ("uiContext", "project"),
                ("zaznamId", s.RecordId.ToString()),
                ("Id", s.CommentId.ToString()),
                ("Text", "<p>podvrh</p>")));

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(await client.SendAsync(request));

        payload.Ok.Should().BeFalse("vyjádření k uzavřenému jednání nejde upravit");
        await using var db = _fixture.CreateDbContext();
        (await db.Vyjadreni.AsNoTracking().SingleAsync(x => x.Id == s.CommentId)).TextVyjadreni
            .Should().Be("ApiClsMtgUpd k uzavrenemu jednani");
    }

    [Fact]
    public async Task DeleteComment_ClosedMeeting_IsRefused_CommentKept()
    {
        var s = await SeedAsync("ApiClsMtgDel", 9613);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/DeleteComment?asUser={s.PmId}",
            ApiTestHttpHelper.BuildForm(
                ("projektId", s.ProjectId.ToString()),
                ("uiContext", "project"),
                ("zaznamId", s.RecordId.ToString()),
                ("Id", s.CommentId.ToString())));

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(await client.SendAsync(request));

        payload.Ok.Should().BeFalse("vyjádření k uzavřenému jednání nejde smazat");
        await using var db = _fixture.CreateDbContext();
        (await db.Vyjadreni.AnyAsync(x => x.Id == s.CommentId)).Should().BeTrue();
    }
}
