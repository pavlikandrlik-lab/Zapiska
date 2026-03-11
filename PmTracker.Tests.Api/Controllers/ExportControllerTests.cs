using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public ExportControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task JednaniTisk_ShouldRenderAttendanceWithoutProjectRoles_AndUseCommentHeaderOrderWithTextColor()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiExportOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIEXP1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIEXPSUB1", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API export record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 551);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var meeting = await dbContext.Jednani.FirstAsync(x => x.Id == meetingId);
            meeting.DatumPlanovane = new DateTime(2026, 2, 17);

            var presentStateId = await dbContext.CiselnikStavuUcasti
                .Where(x => x.Kod == "PRESENT")
                .Select(x => x.Id)
                .FirstAsync();

            dbContext.Ucast.Add(new UcastEntity
            {
                JednaniId = meetingId,
                OsobaId = ownerId,
                StavUcastiId = presentStateId
            });

            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = "<p>Export comment</p>",
                DatumVyjadreni = new DateTime(2026, 2, 18, 9, 0, 0)
            });

            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Export/Jednani/{meetingId}/Tisk?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("class=\"attendance\"");
        html.Should().NotContain("class=\"project-roles\"");
        html.Should().Contain("551 (17.02.2026) | Ing. ApiExportOwner Api | 18.02.2026");
        html.Should().Contain("class=\"comment-item\" style=\"color:#0F4D8A;\"");
        html.Should().NotContain("comment-item highlight");
        html.Should().NotContain("style=\"background:");
    }
}
