using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class JednaniControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public JednaniControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TaskItemPartial_ShouldRenderTaskItem_WhenUserCanEditRecords()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_TASK_OK");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/TaskItemPartial?jednaniId={context.MeetingId}&zaznamId={context.RecordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain($"data-task-record-id=\"{context.RecordId}\"");
        html.Should().Contain("meeting-note-add-form");
    }

    [Fact]
    public async Task TaskItemPartial_ShouldReturnNotFound_WhenTaskDoesNotExist()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_TASK_404");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/TaskItemPartial?jednaniId={context.MeetingId}&zaznamId=999999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TaskItemPartial_ShouldReturnForbidden_WhenUserCanReadProjectButCannotEditOrComment()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_TASK_403");
        var limitedUserId = await _fixture.EnsurePersonAsync("ApiMeetingTaskHostOnly");
        await AssignProjectRoleAsync(context.ProjectId, limitedUserId, ProjectRoleCodes.Host);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/TaskItemPartial?jednaniId={context.MeetingId}&zaznamId={context.RecordId}&asUser={limitedUserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TaskItemPartial_ShouldRenderTaskItem_WhenUserIsSubsystemLeadWithoutRecordsEdit_InDraft()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_TASK_LEAD", "DRAFT");
        var leadUserId = await _fixture.EnsurePersonAsync("ApiMeetingTaskSubsystemLead");
        var subsystemId = await GetRecordSubsystemIdAsync(context.RecordId);
        await AssignSubsystemRoleAsync(context.ProjectId, subsystemId, leadUserId, SubsystemRoleCodes.Lead);
        await GrantProjectPermissionAsync(context.ProjectId, leadUserId, PermissionKeys.RecordsCommentSubsystemLead, "ApiTaskLead");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/TaskItemPartial?jednaniId={context.MeetingId}&zaznamId={context.RecordId}&asUser={leadUserId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain($"data-task-record-id=\"{context.RecordId}\"");
        html.Should().Contain("meeting-note-add-form");
    }

    [Fact]
    public async Task TaskItemPartial_ShouldRenderWithoutAddOrEdit_WhenSubsystemLeadWithoutRecordsEdit_OpensOpenMeeting()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_TASK_LEAD_OPEN", "OPEN");
        var leadUserId = await _fixture.EnsurePersonAsync("ApiMeetingTaskSubsystemLeadOpen");
        var subsystemId = await GetRecordSubsystemIdAsync(context.RecordId);
        await AssignSubsystemRoleAsync(context.ProjectId, subsystemId, leadUserId, SubsystemRoleCodes.Lead);
        await GrantProjectPermissionAsync(context.ProjectId, leadUserId, PermissionKeys.RecordsCommentSubsystemLead, "ApiTaskLeadOpen");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = context.RecordId,
                JednaniId = context.MeetingId,
                AutorOsobaId = leadUserId,
                TextVyjadreni = "Subsystem lead open comment",
                DatumVyjadreni = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/TaskItemPartial?jednaniId={context.MeetingId}&zaznamId={context.RecordId}&asUser={leadUserId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain($"data-task-record-id=\"{context.RecordId}\"");
        html.Should().NotContain("meeting-note-add-form");
        html.Should().NotContain("comment-edit-form");
        html.Should().NotContain("comment-delete-form");
    }

    [Fact]
    public async Task TaskItemPartial_ShouldKeepCommentEditDeleteVisible_ForRecordsEditUserInOpenMeeting()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_TASK_EDIT_OPEN", "OPEN");
        var commenterId = await _fixture.EnsurePersonAsync("ApiMeetingTaskCommentAuthor");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = context.RecordId,
                JednaniId = context.MeetingId,
                AutorOsobaId = commenterId,
                TextVyjadreni = "Existing editable comment",
                DatumVyjadreni = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/TaskItemPartial?jednaniId={context.MeetingId}&zaznamId={context.RecordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("comment-edit-form");
        html.Should().Contain("comment-delete-form");
    }

    [Fact]
    public async Task TaskItemPartial_ShouldReturnNotFound_WhenUserCannotAccessProject()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_TASK_NOACCESS");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiMeetingTaskNoAccess");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/TaskItemPartial?jednaniId={context.MeetingId}&zaznamId={context.RecordId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMeetingParticipantModal_ShouldRenderModal_WhenUserHasMeetingsEditPermission()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_MODAL_OK");
        var participantId = await _fixture.EnsurePersonAsync("ApiMeetingModalCandidate");
        await _fixture.EnsureProjectTeamMemberAsync(context.ProjectId, participantId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/AddMeetingParticipantModal?projektId={context.ProjectId}&jednaniId={context.MeetingId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("meeting-participant-modal-title");
        html.Should().Contain($"name=\"ProjektId\" value=\"{context.ProjectId}\"");
        html.Should().Contain($"name=\"JednaniId\" value=\"{context.MeetingId}\"");
        html.Should().Contain($"data-id=\"{participantId}\"");
    }

    [Fact]
    public async Task AddMeetingParticipantModal_ShouldReturnForbidden_WhenUserLacksMeetingsEditPermission()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_MODAL_403");
        var userId = await _fixture.EnsurePersonAsync("ApiMeetingModalForbidden");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/AddMeetingParticipantModal?projektId={context.ProjectId}&jednaniId={context.MeetingId}&asUser={userId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddMeetingParticipantModal_ShouldReturnNotFound_WhenMeetingBelongsToDifferentProject()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_MODAL_404");
        var differentProjectId = await _fixture.EnsureProjectAsync("APIMTG_MODAL_OTHER");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/AddMeetingParticipantModal?projektId={differentProjectId}&jednaniId={context.MeetingId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMeetingParticipantModal_ShouldReturnNotFound_WhenMeetingDoesNotExist()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMTG_MODAL_MISSING");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/AddMeetingParticipantModal?projektId={projectId}&jednaniId=999999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SaveStatus_ShouldUpdateMeetingStatus_AndRedirectToLocalReturnUrl()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_STATUS_OK");
        var currentStatusCode = await GetMeetingStatusCodeAsync(context.MeetingId);
        var targetStatusCode = await GetDifferentMeetingStatusCodeAsync(currentStatusCode);
        var returnUrl = $"/Jednani/Detail/{context.MeetingId}?tab=status";

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveStatus?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("JednaniId", context.MeetingId.ToString()),
                ("Stav", targetStatusCode),
                ("returnUrl", returnUrl))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be(returnUrl);

        var savedStatusCode = await GetMeetingStatusCodeAsync(context.MeetingId);
        savedStatusCode.Should().Be(targetStatusCode);
    }

    [Fact]
    public async Task SaveStatus_ShouldReturnForbidden_WhenUserLacksMeetingsEditPermission()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_STATUS_403");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiMeetingStatusForbidden");
        var originalStatusCode = await GetMeetingStatusCodeAsync(context.MeetingId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveStatus?asUser={outsiderId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("JednaniId", context.MeetingId.ToString()),
                ("Stav", originalStatusCode))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetMeetingStatusCodeAsync(context.MeetingId)).Should().Be(originalStatusCode);
    }

    [Fact]
    public async Task SaveStatus_ShouldRedirectToDetail_WhenStatusIsInvalid_AndReturnUrlIsNotLocal()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_STATUS_CATCH");
        var originalStatusCode = await GetMeetingStatusCodeAsync(context.MeetingId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveStatus?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("JednaniId", context.MeetingId.ToString()),
                ("Stav", "INVALID_STATUS"),
                ("returnUrl", "https://example.com/not-local"))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().StartWith($"/Jednani/Detail/{context.MeetingId}");
        (await GetMeetingStatusCodeAsync(context.MeetingId)).Should().Be(originalStatusCode);
    }

    [Fact]
    public async Task SaveAttendance_ShouldPersistOnlyValidRows_AndRedirectToReturnUrl()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ATT_OK");
        var personValidId = await _fixture.EnsurePersonAsync("ApiMeetingAttendanceValid");
        var personSkippedId = await _fixture.EnsurePersonAsync("ApiMeetingAttendanceSkipped");
        var validAttendanceStatus = await GetAttendanceStatusCodeAsync();
        var returnUrl = $"/Jednani/Detail/{context.MeetingId}?tab=attendance";

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveAttendance?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("projektId", context.ProjectId.ToString()),
                ("jednaniId", context.MeetingId.ToString()),
                ("returnUrl", returnUrl),
                ("rows[0].OsobaId", personValidId.ToString()),
                ("rows[0].StavUcasti", validAttendanceStatus),
                ("rows[1].OsobaId", personSkippedId.ToString()),
                ("rows[1].StavUcasti", string.Empty))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be(returnUrl);

        await using var dbContext = _fixture.CreateDbContext();
        var savedValidStateCode = await dbContext.Ucast.AsNoTracking()
            .Where(x => x.JednaniId == context.MeetingId && x.OsobaId == personValidId)
            .Join(
                dbContext.CiselnikStavuUcasti.AsNoTracking(),
                attendance => attendance.StavUcastiId,
                state => state.Id,
                (_, state) => state.Kod)
            .SingleAsync();
        savedValidStateCode.Should().Be(validAttendanceStatus);

        var skippedExists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == personSkippedId);
        skippedExists.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAttendance_ShouldReturnForbidden_WhenUserLacksMeetingsEditPermission()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ATT_403");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiMeetingAttendanceForbidden");
        var personId = await _fixture.EnsurePersonAsync("ApiMeetingAttendanceForbiddenPerson");
        var validAttendanceStatus = await GetAttendanceStatusCodeAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveAttendance?asUser={outsiderId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("projektId", context.ProjectId.ToString()),
                ("jednaniId", context.MeetingId.ToString()),
                ("rows[0].OsobaId", personId.ToString()),
                ("rows[0].StavUcasti", validAttendanceStatus))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceExists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == personId);
        attendanceExists.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAttendance_ShouldRedirectToDetail_WhenAttendanceStatusIsInvalid()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ATT_INVALID");
        var personId = await _fixture.EnsurePersonAsync("ApiMeetingAttendanceInvalid");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveAttendance?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("projektId", context.ProjectId.ToString()),
                ("jednaniId", context.MeetingId.ToString()),
                ("rows[0].OsobaId", personId.ToString()),
                ("rows[0].StavUcasti", "NOT_A_VALID_STATE"))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().StartWith($"/Jednani/Detail/{context.MeetingId}");

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceExists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == personId);
        attendanceExists.Should().BeFalse();
    }

    [Fact]
    public async Task SaveNotes_ShouldPersistOnlyRowsWithText_AndRedirectToReturnUrl()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_NOTES_OK");
        var returnUrl = $"/Jednani/Detail/{context.MeetingId}?tab=notes";
        const string expectedText = "API meeting note";

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveNotes?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("projektId", context.ProjectId.ToString()),
                ("jednaniId", context.MeetingId.ToString()),
                ("returnUrl", returnUrl),
                ("rows[0].ZaznamId", context.RecordId.ToString()),
                ("rows[0].Text", expectedText),
                ("rows[1].ZaznamId", context.RecordId.ToString()),
                ("rows[1].Text", string.Empty))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().Be(returnUrl);

        await using var dbContext = _fixture.CreateDbContext();
        var notes = await dbContext.Vyjadreni.AsNoTracking()
            .Where(x => x.JednaniId == context.MeetingId && x.ZaznamId == context.RecordId)
            .OrderBy(x => x.Id)
            .Select(x => x.TextVyjadreni)
            .ToListAsync();

        notes.Should().HaveCount(1);
        notes[0].Should().Be(expectedText);
    }

    [Fact]
    public async Task SaveNotes_ShouldReturnForbidden_WhenUserLacksRecordsEditPermission()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_NOTES_403");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiMeetingNotesForbidden");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveNotes?asUser={outsiderId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("projektId", context.ProjectId.ToString()),
                ("jednaniId", context.MeetingId.ToString()),
                ("rows[0].ZaznamId", context.RecordId.ToString()),
                ("rows[0].Text", "Forbidden save notes"))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var dbContext = _fixture.CreateDbContext();
        var notesCount = await dbContext.Vyjadreni.AsNoTracking()
            .CountAsync(x => x.JednaniId == context.MeetingId && x.ZaznamId == context.RecordId);
        notesCount.Should().Be(0);
    }

    [Fact]
    public async Task SaveNotes_ShouldRedirectToDetail_WhenSaveMeetingNoteFails_AndReturnUrlIsNotLocal()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_NOTES_CATCH");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/SaveNotes?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("projektId", context.ProjectId.ToString()),
                ("jednaniId", context.MeetingId.ToString()),
                ("returnUrl", "https://example.com/not-local"),
                ("rows[0].ZaznamId", "999999999"),
                ("rows[0].Text", "Will fail"))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().StartWith($"/Jednani/Detail/{context.MeetingId}");

        await using var dbContext = _fixture.CreateDbContext();
        var notesCount = await dbContext.Vyjadreni.AsNoTracking()
            .CountAsync(x => x.JednaniId == context.MeetingId);
        notesCount.Should().Be(0);
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldReturnAjaxSuccess_AndPersistAttendance()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_OK");
        var participantId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantAdd");
        await _fixture.EnsureProjectTeamMemberAsync(context.ProjectId, participantId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Jednani/AddMeetingParticipant?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()),
                ("OsobaId", participantId.ToString())));

        var response = await client.SendAsync(request);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("page");
        payload.MeetingId.Should().Be(context.MeetingId);
        payload.ProjectId.Should().Be(context.ProjectId);

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceExists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == participantId);
        attendanceExists.Should().BeTrue();
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldReturnAjaxValidationError_WhenPersonIsMissing()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_VALIDATION");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Jednani/AddMeetingParticipant?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString())));

        var response = await client.SendAsync(request);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.RequestValidationFailed);
        payload.Message.Should().Contain("Osobu nelze přidat do účasti.");
        payload.FieldErrors.Keys.Should().Contain(key => key.Contains("OsobaId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldReturnAjaxForbidden_WhenUserLacksPermission()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_403");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantForbidden");
        var participantId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantForbiddenCandidate");
        await _fixture.EnsureProjectTeamMemberAsync(context.ProjectId, participantId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Jednani/AddMeetingParticipant?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()),
                ("OsobaId", participantId.ToString())));

        var response = await client.SendAsync(request);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Nemáte oprávnění");

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceExists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == participantId);
        attendanceExists.Should().BeFalse();
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldReturnAjaxError_WhenMeetingDoesNotBelongToProject()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_MISMATCH");
        var differentProjectId = await _fixture.EnsureProjectAsync("APIMTG_ADD_OTHER_PROJECT");
        var participantId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantMismatchCandidate");
        await _fixture.EnsureProjectTeamMemberAsync(context.ProjectId, participantId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Jednani/AddMeetingParticipant?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", differentProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()),
                ("OsobaId", participantId.ToString())));

        var response = await client.SendAsync(request);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("nepatří do zvoleného projektu");
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldRedirectToDetail_WhenNonAjaxRequestSucceeds()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_NONAJAX_OK");
        var participantId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantNonAjaxOk");
        await _fixture.EnsureProjectTeamMemberAsync(context.ProjectId, participantId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/AddMeetingParticipant?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()),
                ("OsobaId", participantId.ToString()))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().StartWith($"/Jednani/Detail/{context.MeetingId}");

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceExists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == participantId);
        attendanceExists.Should().BeTrue();
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldRedirectToDetail_WhenNonAjaxRequestIsInvalid()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_NONAJAX_VALIDATION");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/AddMeetingParticipant?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().StartWith($"/Jednani/Detail/{context.MeetingId}");

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceCount = await dbContext.Ucast.AsNoTracking()
            .CountAsync(x => x.JednaniId == context.MeetingId);
        attendanceCount.Should().Be(0);
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldReturnForbidden_WhenNonAjaxUserLacksPermission()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_NONAJAX_403");
        var outsiderId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantNonAjaxForbidden");
        var participantId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantNonAjaxForbiddenCandidate");
        await _fixture.EnsureProjectTeamMemberAsync(context.ProjectId, participantId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/AddMeetingParticipant?asUser={outsiderId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()),
                ("OsobaId", participantId.ToString()))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceCount = await dbContext.Ucast.AsNoTracking()
            .CountAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == participantId);
        attendanceCount.Should().Be(0);
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldRedirectToDetail_WhenNonAjaxOperationFails()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_NONAJAX_FAIL");
        var outsiderParticipantId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantNonAjaxFailCandidate");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/AddMeetingParticipant?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()),
                ("OsobaId", outsiderParticipantId.ToString()))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().StartWith($"/Jednani/Detail/{context.MeetingId}");

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceCount = await dbContext.Ucast.AsNoTracking()
            .CountAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == outsiderParticipantId);
        attendanceCount.Should().Be(0);
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldRemainSingleAttendanceRow_WhenParticipantAlreadyExists()
    {
        var context = await CreateMeetingTaskContextAsync("APIMTG_ADD_IDEMPOTENT");
        var participantId = await _fixture.EnsurePersonAsync("ApiMeetingParticipantIdempotent");
        await _fixture.EnsureProjectTeamMemberAsync(context.ProjectId, participantId);
        await EnsureAttendanceRowAsync(context.MeetingId, participantId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Jednani/AddMeetingParticipant?asUser={_fixture.AdminOsobaId}")
        {
            Content = ApiTestHttpHelper.BuildForm(
                ("ProjektId", context.ProjectId.ToString()),
                ("JednaniId", context.MeetingId.ToString()),
                ("OsobaId", participantId.ToString()))
        };

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location?.OriginalString.Should().StartWith($"/Jednani/Detail/{context.MeetingId}");

        await using var dbContext = _fixture.CreateDbContext();
        var attendanceCount = await dbContext.Ucast.AsNoTracking()
            .CountAsync(x => x.JednaniId == context.MeetingId && x.OsobaId == participantId);
        attendanceCount.Should().Be(1);
    }

    private async Task<MeetingTaskContext> CreateMeetingTaskContextAsync(string marker, string stateCode = "OPEN")
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}_OWNER");
        var projectId = await _fixture.EnsureProjectAsync($"{marker}_PROJ");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}_SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} record");
        var meetingId = await CreateMeetingWithNextNumberAsync(projectId, stateCode);

        return new MeetingTaskContext(projectId, meetingId, recordId);
    }

    private async Task<int> CreateMeetingWithNextNumberAsync(int projectId, string stateCode)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var nextNumber = (await dbContext.Jednani.AsNoTracking()
            .Where(x => x.ProjektId == projectId)
            .Select(x => (int?)x.CisloJednani)
            .MaxAsync() ?? 0) + 1;

        return await _fixture.CreateMeetingAsync(projectId, stateCode, nextNumber);
    }

    private async Task AssignProjectRoleAsync(int projectId, int osobaId, string roleCode)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var roleId = await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == roleCode)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        roleId.Should().NotBeNull();

        var existing = await dbContext.ObsazeniProjektu
            .FirstOrDefaultAsync(x =>
                x.ProjektId == projectId &&
                x.OsobaId == osobaId &&
                x.RoleId == roleId!.Value &&
                !x.DatumOdebrani.HasValue);
        if (existing is not null)
        {
            return;
        }

        dbContext.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            ProjektId = projectId,
            OsobaId = osobaId,
            RoleId = roleId.Value,
            DatumPrirazeni = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task AssignSubsystemRoleAsync(int projectId, int subsystemId, int osobaId, string roleCode)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var roleId = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == roleCode)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        roleId.Should().NotBeNull();

        var projectSubsystemId = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        projectSubsystemId.Should().NotBeNull();

        var existing = await dbContext.ObsazeniSubsystemuProjektu
            .FirstOrDefaultAsync(x =>
                x.ProjektSubsystemId == projectSubsystemId!.Value &&
                x.OsobaId == osobaId &&
                x.RoleSubsystemuId == roleId!.Value &&
                !x.DatumOdebrani.HasValue);
        if (existing is not null)
        {
            return;
        }

        dbContext.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            ProjektSubsystemId = projectSubsystemId.Value,
            OsobaId = osobaId,
            RoleSubsystemuId = roleId.Value,
            DatumPrirazeni = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<int> GetRecordSubsystemIdAsync(int recordId)
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.ProjektoveZaznamy.AsNoTracking()
            .Where(x => x.Id == recordId)
            .Select(x => x.SubsystemId)
            .SingleAsync();
    }

    private async Task EnsureAttendanceRowAsync(int meetingId, int osobaId)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var exists = await dbContext.Ucast.AsNoTracking()
            .AnyAsync(x => x.JednaniId == meetingId && x.OsobaId == osobaId);
        if (exists)
        {
            return;
        }

        var statusId = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        dbContext.Ucast.Add(new UcastEntity
        {
            JednaniId = meetingId,
            OsobaId = osobaId,
            StavUcastiId = statusId
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task GrantProjectPermissionAsync(int projectId, int osobaId, string permissionKey, string roleMarker)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var permissionId = await dbContext.AuthzPermissions.AsNoTracking()
            .Where(x => x.IsActive && x.Klic == permissionKey)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        permissionId.Should().NotBeNull();

        var roleCode = $"API_{roleMarker}_{Guid.NewGuid():N}".ToUpperInvariant();
        roleCode = roleCode.Length <= 40 ? roleCode : roleCode[..40];

        var role = new AuthzRoleEntity
        {
            Kod = roleCode,
            Nazev = roleMarker,
            Popis = roleMarker,
            IsSystem = false,
            IsActive = true
        };
        dbContext.AuthzRoles.Add(role);
        await dbContext.SaveChangesAsync();

        var rolePermission = new AuthzRolePermissionEntity
        {
            RoleId = role.Id,
            PermissionId = permissionId.Value,
            ScopeMode = "INCLUDE",
            IsAllowed = true
        };
        dbContext.AuthzRolePermissions.Add(rolePermission);
        await dbContext.SaveChangesAsync();

        dbContext.AuthzRolePermissionProjects.Add(new AuthzRolePermissionProjectEntity
        {
            RolePermissionId = rolePermission.Id,
            ProjektId = projectId
        });

        dbContext.AuthzUserRoles.Add(new AuthzUserRoleEntity
        {
            OsobaId = osobaId,
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private async Task<string> GetMeetingStatusCodeAsync(int meetingId)
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.Jednani.AsNoTracking()
            .Where(x => x.Id == meetingId)
            .Join(
                dbContext.CiselnikStavuJednani.AsNoTracking(),
                meeting => meeting.StavJednaniId,
                state => state.Id,
                (_, state) => state.Kod)
            .SingleAsync();
    }

    private async Task<string> GetDifferentMeetingStatusCodeAsync(string currentStatusCode)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var statusCodes = await dbContext.CiselnikStavuJednani.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .ToListAsync();

        return statusCodes.FirstOrDefault(x => !string.Equals(x, currentStatusCode, StringComparison.OrdinalIgnoreCase))
            ?? currentStatusCode;
    }

    private async Task<string> GetAttendanceStatusCodeAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var preferred = await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .Where(x => x.Kod == "PRESENT")
            .Select(x => x.Kod)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        return await dbContext.CiselnikStavuUcasti.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
    }

    private sealed record MeetingTaskContext(int ProjectId, int MeetingId, int RecordId);
}
