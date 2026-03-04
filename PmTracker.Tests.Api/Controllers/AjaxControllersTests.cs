using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class AjaxControllersTests
{
    private readonly ApiSqlFixture _fixture;

    public AjaxControllersTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveMeeting_ShouldReturnFieldErrors_WhenMeetingTimeIsInvalid()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT1");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveMeeting?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("CisloJednani", "123"),
                ("DatumPlanovane", "2026-02-18"),
                ("CasZacatek", "invalid"),
                ("Misto", "Test"),
                ("StavJednani", "DRAFT")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.FieldErrors.Keys.Should().Contain(key => key.Contains("CasZacatek", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EditMeetingModal_ShouldRenderExistingMeetingValues_ForOpenMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT_EDIT_GET");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 126);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/EditMeetingModal?projektId={projectId}&meetingId={meetingId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Upravit poradu");
        html.Should().Contain($"name=\"Id\" value=\"{meetingId}\"");
        html.Should().Contain("data-current-meeting-number=\"126\"");
    }

    [Fact]
    public async Task SaveMeeting_ShouldUpdateExistingMeeting_ForOpenMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT_EDIT_POST");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 127);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveMeeting?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", meetingId.ToString()),
                ("ProjektId", projectId.ToString()),
                ("CisloJednani", "128"),
                ("DatumPlanovane", "2026-03-04"),
                ("CasZacatek", "13:15"),
                ("Misto", "Upraveno"),
                ("StavJednani", "OPEN")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("projekty-detail-jednani");

        await using var dbContext = _fixture.CreateDbContext();
        var meeting = await dbContext.Jednani.SingleAsync(x => x.Id == meetingId);
        meeting.CisloJednani.Should().Be(128);
        meeting.CasZacatek.Should().Be(new TimeOnly(13, 15));
        meeting.Misto.Should().Be("Upraveno");
    }

    [Fact]
    public async Task EditMeetingModal_ShouldReturnForbidden_ForClosedMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT_EDIT_CLOSED");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", 129);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/EditMeetingModal?projektId={projectId}&meetingId={meetingId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteMeeting_ShouldReturnAjaxSuccessAndDeleteMeeting()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMT2");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 124);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeleteMeeting?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("JednaniId", meetingId.ToString()),
                ("ProjektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("projekty-detail-jednani");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Jednani.AnyAsync(x => x.Id == meetingId)).Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_ShouldReturnAjaxSuccess_ForProjectContext()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiCommentOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIMT3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APISUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Api Comment Record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 125);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AddComment?projektId={projectId}&asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString()),
                ("Text", "API comment"),
                ("uiContext", "project")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("record-card");
        payload.RecordId.Should().Be(recordId);

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.Vyjadreni.AnyAsync(x => x.ZaznamId == recordId && x.JednaniId == meetingId)).Should().BeTrue();
    }

    [Fact]
    public async Task SavePermission_ShouldReturnAjaxError_ForUnsupportedKey()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var categoryId = await dbContext.AuthzPermissionCategories.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SavePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Klic", "unsupported.key"),
                ("Nazev", "Unsupported"),
                ("CategoryId", categoryId.ToString()),
                ("ScopeLevel", "PROJECT")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.Message.Should().Contain("není v seznamu podporovaných akcí");
    }
}
