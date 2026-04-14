using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingIdentifierFlowControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingIdentifierFlowControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AssignMeetingIdentifierModal_ShouldRenderModalWithMeetingOptions_WhenUserCanEditRecord()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingModalOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAMOD1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAMODSUB1", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting modal record");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 2101);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/AssignMeetingIdentifierModal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("assign-meeting-identifier-modal-title");
        decodedHtml.Should().Contain("Doplnit identifikátor z jednání");
        html.Should().Contain($"name=\"ProjektId\" value=\"{projectId}\"");
        html.Should().Contain($"name=\"ZaznamId\" value=\"{recordId}\"");
        decodedHtml.Should().Contain("Aktuální číslo záznamu");
        html.Should().Contain("name=\"JednaniId\"");
        html.Should().Contain($"<option value=\"{meetingId}\"");
    }

    [Fact]
    public async Task AssignMeetingIdentifierModal_ShouldReturnForbidden_WhenUserLacksEditPermission()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingModalNoPermOwner");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiAssignMeetingModalOutsider");
        var projectId = await _fixture.EnsureProjectAsync("APIAMOD2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAMODSUB2", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting modal no perm");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/AssignMeetingIdentifierModal?projektId={projectId}&zaznamId={recordId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignMeetingIdentifierModal_ShouldReturnNotFound_WhenRecordDoesNotBelongToProject()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingModalNotFoundOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAMOD3");
        var differentProjectId = await _fixture.EnsureProjectAsync("APIAMOD4");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAMODSUB3", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting modal mismatch");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/AssignMeetingIdentifierModal?projektId={differentProjectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignMeetingIdentifierModal_ShouldDisableSubmit_WhenNoOpenMeetingsExist()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingModalNoOpenOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAMOD5");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAMODSUB5", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting modal no open");
        await _fixture.CreateMeetingAsync(projectId, "CLOSED", 2106);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/AssignMeetingIdentifierModal?projektId={projectId}&zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Není dostupné žádné neuzavřené jednání.");
        Regex.IsMatch(
            html,
            "<select[^>]*name=\"JednaniId\"[^>]*\\bdisabled(?:=\"disabled\")?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Should().BeTrue("select musí být bez otevřených jednání deaktivovaný");
        Regex.IsMatch(
            html,
            "<button[^>]*type=\"submit\"[^>]*\\bdisabled(?:=\"disabled\")?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Should().BeTrue("submit musí být bez otevřených jednání deaktivovaný");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnAjaxSuccess_AndPersistMeetingIdentifier()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingPostOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB1", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting post success");
        const int meetingNumber = 2102;
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", meetingNumber);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var initialRecord = await dbContext.ProjektoveZaznamy.AsNoTracking()
                .SingleAsync(x => x.Id == recordId);
            initialRecord.CisloViditelneTyp.Should().NotBe((byte)1);
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.Message.Should().Be("Identifikátor z jednání byl doplněn.");
        payload.RefreshScope.Should().Be("record-card");
        payload.RefreshUrl.Should().Contain("/Zaznamy/RecordCardPartial");
        payload.RefreshUrl.Should().Contain($"projektId={projectId}");
        payload.RefreshUrl.Should().Contain($"zaznamId={recordId}");
        payload.ProjectId.Should().Be(projectId);
        payload.RecordId.Should().Be(recordId);
        payload.UiContext.Should().Be("project");
        payload.Tab.Should().Be("zaznamy");

        await using var verificationDbContext = _fixture.CreateDbContext();
        var updated = await verificationDbContext.ProjektoveZaznamy.AsNoTracking()
            .SingleAsync(x => x.Id == recordId);
        updated.CisloViditelneTyp.Should().Be((byte)1);
        updated.CisloJednaniZdrojId.Should().Be(meetingId);
        updated.CisloViditelneA.Should().Be(meetingNumber);
        updated.CisloViditelneB.Should().BeGreaterThan(0);
        updated.CisloViditelne.Should().Be($"{meetingNumber}-{updated.CisloViditelneB}");

        var auditExists = await verificationDbContext.AuthzAuditLog.AnyAsync(x =>
            x.EntityType == "zaznam"
            && x.EntityId == recordId.ToString()
            && x.Action == "assign");
        auditExists.Should().BeTrue();
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnJsonForbiddenPayload_WhenUserLacksPermission()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingNoPermOwner");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiAssignMeetingNoPermOutsider");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB2", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting forbidden");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 2103);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, content);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Nemáte oprávnění");
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
        payload.DiagnosticLog.Should().Contain("Permission check failed");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnForbidden_WhenRequestIsNotAjaxAndUserLacksPermission()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingNoAjaxNoPermOwner");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiAssignMeetingNoAjaxNoPermOutsider");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST7");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB7", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting no ajax forbidden");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 2107);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsync(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString())));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnValidationError_WhenMeetingIdBindingFails()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingValidationOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB3", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting validation");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", "neplatne-id")));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("REQUEST_VALIDATION_FAILED");
        payload.Message.Should().Be("Identifikátor z jednání nelze doplnit.");
        payload.FieldErrors.Keys.Should().Contain(key => key.Contains("JednaniId", StringComparison.OrdinalIgnoreCase));
        payload.DiagnosticLog.Should().Contain("ModelState validation failed");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnOperationFailed_WhenMeetingIsClosed()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingClosedOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST4");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB4", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting closed");
        var closedMeetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", 2104);

        await using var initialDbContext = _fixture.CreateDbContext();
        var initial = await initialDbContext.ProjektoveZaznamy.AsNoTracking()
            .SingleAsync(x => x.Id == recordId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", closedMeetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("uzavřené");

        await using var verificationDbContext = _fixture.CreateDbContext();
        var unchanged = await verificationDbContext.ProjektoveZaznamy.AsNoTracking()
            .SingleAsync(x => x.Id == recordId);
        unchanged.CisloViditelneTyp.Should().Be(initial.CisloViditelneTyp);
        unchanged.CisloJednaniZdrojId.Should().Be(initial.CisloJednaniZdrojId);
        unchanged.CisloViditelne.Should().Be(initial.CisloViditelne);
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnOperationFailed_WhenRecordDoesNotExist()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingNotFoundOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST8");
        await _fixture.EnsureSubsystemAsync("APIAPOSTSUB8", ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 2108);
        var nonExistingRecordId = int.MaxValue - 1000;

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", nonExistingRecordId.ToString()),
                ("JednaniId", meetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("nebyl nalezen");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnOperationFailed_WhenRecordBelongsToDifferentProject()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingMismatchOwner");
        var firstProjectId = await _fixture.EnsureProjectAsync("APIAPOST9");
        var secondProjectId = await _fixture.EnsureProjectAsync("APIAPOST10");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB9", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(firstProjectId, ownerId, subsystemId, "U", "API assign meeting mismatch");
        var meetingId = await _fixture.CreateMeetingAsync(secondProjectId, "OPEN", 2109);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", secondProjectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("nepatří do vybraného projektu");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnOperationFailed_WhenRecordAlreadyHasMeetingIdentifier()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingAlreadyAssignedOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST11");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB11", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting already assigned");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 2110);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var record = await dbContext.ProjektoveZaznamy.SingleAsync(x => x.Id == recordId);
            record.CisloViditelneTyp = 1;
            record.CisloViditelneA = 2110;
            record.CisloViditelneB = 1;
            record.CisloJednaniZdrojId = meetingId;
            record.CisloViditelne = "2110-1";
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("už má identifikátor podle jednání");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnOperationFailed_WhenNoOpenMeetingsExist()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingNoOpenOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST12");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB12", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting no open");
        var closedMeetingId = await _fixture.CreateMeetingAsync(projectId, "CLOSED", 2111);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", closedMeetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Není dostupné žádné neuzavřené jednání");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldReturnOperationFailed_WhenSelectedMeetingDoesNotExist()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingMissingMeetingOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST13");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB13", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting missing meeting");
        var existingMeetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 2112);
        var missingMeetingId = existingMeetingId + 100_000;

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", missingMeetingId.ToString())));

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be("OPERATION_FAILED");
        payload.Message.Should().Contain("Vybrané jednání neexistuje");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldRedirectToProjectDetail_WhenRequestIsNotAjaxAndSucceeds()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingRedirectOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST5");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB5", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting redirect success");
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 2105);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsync(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", meetingId.ToString())));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("/Projekty/Detail");
        location.Should().Contain(projectId.ToString());
        location.Should().Contain("tab=zaznamy");
    }

    [Fact]
    public async Task AssignMeetingIdentifier_ShouldRedirectToProjectDetail_WhenRequestIsNotAjaxAndModelIsInvalid()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiAssignMeetingRedirectInvalidOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIAPOST6");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIAPOSTSUB6", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API assign meeting redirect invalid");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsync(
            $"/Zaznamy/AssignMeetingIdentifier?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ZaznamId", recordId.ToString()),
                ("JednaniId", "neplatne-id")));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        var location = response.Headers.Location!.ToString();
        location.Should().Contain("/Projekty/Detail");
        location.Should().Contain(projectId.ToString());
        location.Should().Contain("tab=zaznamy");
    }
}
