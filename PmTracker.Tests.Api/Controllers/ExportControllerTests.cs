using System.Net;
using System.Threading;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExportControllerTests
{
    private const string WordContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private static int _meetingSequence = 7000;
    private readonly ApiSqlFixture _fixture;

    public ExportControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProjektTisk_ShouldRedirectToProjectsIndex_WhenProjectDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Export/Projekt/999999/Tisk?asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/");
    }

    [Fact]
    public async Task ReadEndpoints_ShouldReturnNotFound_WhenUserCannotAccessProject()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiExportNoRead");
        var data = await CreateExportScenarioAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var routes = new[]
        {
            $"/Export/Projekt/{data.ProjectId}/Tisk?asUser={userId}",
            $"/Export/Projekt/{data.ProjectId}/Word?asUser={userId}",
            $"/Export/Jednani/{data.MeetingId}/Tisk?asUser={userId}",
            $"/Export/Jednani/{data.MeetingId}/Word?asUser={userId}",
            $"/Export/Ukol/{data.RecordId}/Tisk?projektId={data.ProjectId}&asUser={userId}",
            $"/Export/Ukol/{data.RecordId}/Word?projektId={data.ProjectId}&asUser={userId}"
        };

        foreach (var route in routes)
        {
            var response = await client.GetAsync(route);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, $"route {route} musí skrývat nepřístupný projekt");
        }
    }

    [Fact]
    public async Task ReadEndpoints_ShouldReturnExpectedContentTypes_WhenProjectIsReadable()
    {
        var data = await CreateExportScenarioAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var routes = new (string Route, string ExpectedMediaType, bool IsWord)[]
        {
            ($"/Export/Projekt/{data.ProjectId}/Tisk?asUser={_fixture.AdminOsobaId}", "text/html", false),
            ($"/Export/Projekt/{data.ProjectId}/Word?asUser={_fixture.AdminOsobaId}", WordContentType, true),
            ($"/Export/Jednani/{data.MeetingId}/Tisk?asUser={_fixture.AdminOsobaId}", "text/html", false),
            ($"/Export/Jednani/{data.MeetingId}/Word?asUser={_fixture.AdminOsobaId}", WordContentType, true),
            ($"/Export/Ukol/{data.RecordId}/Tisk?projektId={data.ProjectId}&asUser={_fixture.AdminOsobaId}", "text/html", false),
            ($"/Export/Ukol/{data.RecordId}/Word?projektId={data.ProjectId}&asUser={_fixture.AdminOsobaId}", WordContentType, true)
        };

        foreach (var route in routes)
        {
            var response = await client.GetAsync(route.Route);

            response.StatusCode.Should().Be(HttpStatusCode.OK, route.Route);
            response.Content.Headers.ContentType?.MediaType.Should().Be(route.ExpectedMediaType);

            if (!route.IsWord)
            {
                continue;
            }

            var payload = await response.Content.ReadAsByteArrayAsync();
            payload.Should().NotBeEmpty();
            response.Content.Headers.ContentDisposition.Should().NotBeNull();
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName;
            fileName.Should().NotBeNullOrWhiteSpace();
            fileName!.Should().Contain(".docx");
        }
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
            var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
            record.DatumZalozeni = new DateTime(2026, 2, 1);
            record.DatumUkonceni = new DateTime(2026, 2, 28);
            record.StavUkoluId = await dbContext.CiselnikStavuUkolu
                .Where(x => !x.IsFinal)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync();

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
        html.Should().Contain("<th>Záznamy a vyjádření</th>");
        html.Should().Contain("551 (17.02.2026) | Ing. ApiExportOwner Api | 18.02.2026");
        html.Should().Contain("class=\"comment-item\" style=\"color:#0F4D8A;\"");
        html.Should().Contain("content: '\\2022';");
        html.Should().NotContain("comment-item highlight");
        html.Should().NotContain("style=\"background:");
    }

    [Fact]
    public async Task Dialog_ShouldRedirectToProjectsIndex_WhenProjectDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Export/Dialog?projektId=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/");
    }

    [Fact]
    public async Task Dialog_ShouldRedirectToMeetingPrint_WhenMeetingIdIsProvided()
    {
        var data = await CreateExportScenarioAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Dialog?projektId={data.ProjectId}&jednaniId={data.MeetingId}&autoPrint=false&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        GetLocation(response).ToLowerInvariant().Should().Be($"/export/jednani/{data.MeetingId}/tisk?autoprint=false");
    }

    [Fact]
    public async Task Dialog_ShouldRedirectToProjectPrint_WhenMeetingIdIsMissing()
    {
        var data = await CreateExportScenarioAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Export/Dialog?projektId={data.ProjectId}&autoPrint=true&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        GetLocation(response).ToLowerInvariant().Should().Be($"/export/projekt/{data.ProjectId}/tisk?autoprint=true");
    }

    [Fact]
    public async Task Pdf_ShouldRedirectToMeetingPrint_WhenMeetingIdIsProvided()
    {
        var data = await CreateExportScenarioAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var form = ApiTestHttpHelper.BuildForm(
            ("ProjektId", data.ProjectId.ToString()),
            ("JednaniId", data.MeetingId.ToString()));

        var response = await client.PostAsync($"/Export/Pdf?asUser={_fixture.AdminOsobaId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        GetLocation(response).ToLowerInvariant().Should().Be($"/export/jednani/{data.MeetingId}/tisk?autoprint=true");
    }

    [Fact]
    public async Task Pdf_ShouldRedirectToProjectPrint_WhenMeetingIdIsMissing()
    {
        var data = await CreateExportScenarioAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var form = ApiTestHttpHelper.BuildForm(("ProjektId", data.ProjectId.ToString()));

        var response = await client.PostAsync($"/Export/Pdf?asUser={_fixture.AdminOsobaId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        GetLocation(response).ToLowerInvariant().Should().Be($"/export/projekt/{data.ProjectId}/tisk?autoprint=true");
    }

    [Theory]
    [InlineData("/Export/Projekt/1/Tisk")]
    [InlineData("/Export/Dialog?projektId=1")]
    public async Task ExportGetRoutes_ShouldReturnForbiddenAccessPage_WhenUserContextCannotBeResolved(string route)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(AppendAsUser(route, "99999999"));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        html.Should().Contain("access-card");
    }

    [Fact]
    public async Task Pdf_ShouldReturnForbiddenAccessPage_WhenUserContextCannotBeResolved()
    {
        var data = await CreateExportScenarioAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var form = ApiTestHttpHelper.BuildForm(("ProjektId", data.ProjectId.ToString()));

        var response = await client.PostAsync("/Export/Pdf?asUser=99999999", form);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        html.Should().Contain("access-card");
    }

    private async Task<ExportScenarioData> CreateExportScenarioAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var ownerId = await _fixture.EnsurePersonAsync($"ApiExportOwner{suffix}");
        var projectId = await _fixture.EnsureProjectAsync($"AEX{suffix}");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"SX{suffix}", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"Api export {suffix}");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", Interlocked.Increment(ref _meetingSequence));

        return new ExportScenarioData(projectId, meetingId, recordId);
    }

    private static string GetLocation(HttpResponseMessage response)
    {
        response.Headers.Location.Should().NotBeNull();
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    private static string AppendAsUser(string route, string asUser)
    {
        return route.Contains('?', StringComparison.Ordinal)
            ? $"{route}&asUser={asUser}"
            : $"{route}?asUser={asUser}";
    }

    private sealed record ExportScenarioData(int ProjectId, int MeetingId, int RecordId);
}
