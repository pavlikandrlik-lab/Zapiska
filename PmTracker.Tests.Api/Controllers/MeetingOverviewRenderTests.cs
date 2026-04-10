using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingOverviewRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingOverviewRenderTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Index_ShouldRenderYearGroups_WithPreviewCurrentYear_ForProjectMeetings()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMEETYR");
        var currentYear = DateTime.Today.Year;
        var currentMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 901);
        var previousMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 801);
        await UpdateMeetingDateAsync(currentMeetingId, new DateTime(currentYear, 3, 12));
        await UpdateMeetingDateAsync(previousMeetingId, new DateTime(currentYear - 1, 9, 3));

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-meeting-overview=\"year-grouped\"");
        html.Should().Contain($"data-meeting-preview-year=\"{currentYear}\"");
        html.Should().Contain($"data-meeting-year=\"{currentYear}\"");
        html.Should().Contain($"data-meeting-year=\"{currentYear - 1}\"");
        html.Should().Contain("data-meeting-year-state=\"preview\"");
        html.Should().Contain("data-meeting-year-toggle");
        html.Should().Contain("meeting-card");
    }

    [Fact]
    public async Task Detail_MeetingsTab_ShouldRenderYearGroups_WithoutLatestStrip_AndWithPreviewYear()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMEETTAB");
        var currentYear = DateTime.Today.Year;
        var currentMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 902);
        var previousMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 802);
        await UpdateMeetingDateAsync(currentMeetingId, new DateTime(currentYear, 4, 2));
        await UpdateMeetingDateAsync(previousMeetingId, new DateTime(currentYear - 1, 11, 17));

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?tab=jednani&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-meeting-overview=\"year-grouped\"");
        html.Should().Contain($"data-meeting-preview-year=\"{currentYear}\"");
        html.Should().Contain($"data-meeting-year=\"{currentYear}\"");
        html.Should().Contain("data-meeting-year-state=\"preview\"");
    }

    private async Task UpdateMeetingDateAsync(int meetingId, DateTime date)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == meetingId);
        meeting.DatumPlanovane = date.Date;
        await dbContext.SaveChangesAsync();
    }
}
