using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingAttendanceStateResolutionTests
{
    private readonly SqlIntegrationFixture _fixture;

    public MeetingAttendanceStateResolutionTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveMeeting_ShouldResolveDefaultAttendanceState_WhenPresentCodeIsMissing()
    {
        var db = await _fixture.CreateDatabaseAsync("attendance_state_save_meeting");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "StateSaveAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "StateSaveOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "STATESAV");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);

        var attendanceStates = await dbContext.CiselnikStavuUcasti
            .OrderBy(x => x.Id)
            .ToListAsync();

        attendanceStates.Should().NotBeEmpty();
        foreach (var state in attendanceStates)
        {
            if (string.Equals(state.Kod, "PRESENT", StringComparison.OrdinalIgnoreCase))
            {
                state.Kod = $"ALT_{state.Id}";
            }
        }

        attendanceStates[0].Nazev = "Přítomen";
        await dbContext.SaveChangesAsync();

        var meetingId = store.SaveMeeting(new SaveMeetingCommand
        {
            ProjektId = projectId,
            CisloJednani = 9401,
            DatumPlanovane = new DateTime(2026, 3, 4),
            CasZacatek = new TimeOnly(8, 30),
            Misto = "Poradna",
            StavJednani = "OPEN"
        }, currentUser);

        var attendance = await dbContext.Ucast
            .AsNoTracking()
            .Where(x => x.JednaniId == meetingId && x.OsobaId == ownerId)
            .SingleAsync();

        attendance.StavUcastiId.Should().Be(attendanceStates[0].Id);
    }

    [Fact]
    public async Task AddMeetingParticipant_ShouldResolveDefaultAttendanceState_WhenPresentCodeAndNameAreMissing()
    {
        var db = await _fixture.CreateDatabaseAsync("attendance_state_add_participant");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "StateAddAdmin");
        var participantId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "StateAddParticipant");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "STATEADD");
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, participantId, ProjectRoleCodes.ProjectOwner);

        var attendanceStates = await dbContext.CiselnikStavuUcasti
            .OrderBy(x => x.Id)
            .ToListAsync();

        attendanceStates.Should().NotBeEmpty();
        foreach (var state in attendanceStates)
        {
            state.Kod = $"STATE_{state.Id}";
            state.Nazev = $"Stav {state.Id}";
        }

        await dbContext.SaveChangesAsync();

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9402);

        store.AddMeetingParticipant(new AddMeetingParticipantCommand
        {
            ProjektId = projectId,
            JednaniId = meetingId,
            OsobaId = participantId
        }, currentUser);

        var attendance = await dbContext.Ucast
            .AsNoTracking()
            .Where(x => x.JednaniId == meetingId && x.OsobaId == participantId)
            .SingleAsync();

        attendance.StavUcastiId.Should().Be(attendanceStates[0].Id);
    }
}
