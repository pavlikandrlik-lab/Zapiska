using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Meetings;

namespace PmTracker.Tests.Integration.Meetings;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingsModuleTests
{
    private readonly SqlIntegrationFixture _fixture;

    public MeetingsModuleTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MeetingsQueries_ShouldMatchDataStoreDelegation()
    {
        var db = await _fixture.CreateDatabaseAsync("meetings_queries_delegate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var queries = new MeetingsQueries(store);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingsQueriesOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "MTGQRY");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "MTGQRY_SYS", db.AdminOsobaId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "MeetingsQueriesRecord");

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 8401);

        var overviewFromModule = queries.BuildJednaniOverview();
        var overviewFromDataStore = store.BuildJednaniOverview();
        overviewFromDataStore.Should().BeEquivalentTo(overviewFromModule);

        var detailFromModule = queries.BuildJednaniDetail(meetingId);
        var detailFromDataStore = store.BuildJednaniDetail(meetingId);
        detailFromDataStore.Should().BeEquivalentTo(detailFromModule);

        var projectDetailFromModule = queries.BuildProjektDetail(projectId);
        var projectDetailFromDataStore = store.BuildProjektDetail(projectId);
        projectDetailFromDataStore.Should().BeEquivalentTo(projectDetailFromModule);

        queries.ProjektExists(projectId).Should().BeTrue();
    }

    [Fact]
    public async Task MeetingsCommands_ShouldPersistMeetingAndAttendanceChanges()
    {
        var db = await _fixture.CreateDatabaseAsync("meetings_commands_delegate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var commands = new MeetingsCommands(store);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "MTGCMD");
        var meetingStatusCode = await dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.Kod)
            .FirstAsync();
        var attendanceStatusCode = await dbContext.CiselnikStavuUcasti
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.Kod)
            .FirstAsync();

        var meetingId = commands.SaveMeeting(new SaveMeetingCommand
        {
            ProjektId = projectId,
            CisloJednani = 5101,
            DatumPlanovane = new DateTime(2026, 2, 18),
            CasZacatek = new TimeOnly(9, 30),
            Misto = "Meeting module room",
            StavJednani = meetingStatusCode
        }, currentUser);

        var createdMeeting = await dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == meetingId);
        createdMeeting.Should().NotBeNull();
        createdMeeting!.ProjektId.Should().Be(projectId);

        var participantId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingsCmdParticipant");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, participantId, ProjectRoleCodes.ProjectOwner);

        commands.AddMeetingParticipant(new AddMeetingParticipantCommand
        {
            ProjektId = projectId,
            JednaniId = meetingId,
            OsobaId = participantId
        }, currentUser);

        var addedAttendance = await dbContext.Ucast
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.JednaniId == meetingId && item.OsobaId == participantId);
        addedAttendance.Should().NotBeNull();

        commands.SaveAttendance(new SaveAttendanceCommand
        {
            JednaniId = meetingId,
            OsobaId = participantId,
            StavUcasti = attendanceStatusCode
        }, currentUser);

        var expectedAttendanceStatusId = await dbContext.CiselnikStavuUcasti
            .AsNoTracking()
            .Where(item => item.Kod == attendanceStatusCode)
            .Select(item => item.Id)
            .FirstAsync();
        var updatedAttendance = await dbContext.Ucast
            .AsNoTracking()
            .FirstAsync(item => item.JednaniId == meetingId && item.OsobaId == participantId);
        updatedAttendance.StavUcastiId.Should().Be(expectedAttendanceStatusId);

        commands.SaveMeetingStatus(new SaveMeetingStatusCommand
        {
            JednaniId = meetingId,
            Stav = meetingStatusCode
        }, currentUser);

        var expectedMeetingStatusId = await dbContext.CiselnikStavuJednani
            .AsNoTracking()
            .Where(item => item.Kod == meetingStatusCode)
            .Select(item => item.Id)
            .FirstAsync();
        var updatedMeeting = await dbContext.Jednani
            .AsNoTracking()
            .FirstAsync(item => item.Id == meetingId);
        updatedMeeting.StavJednaniId.Should().Be(expectedMeetingStatusId);

        commands.DeleteMeeting(new DeleteMeetingCommand
        {
            ProjektId = projectId,
            JednaniId = meetingId
        }, currentUser);

        var deletedMeeting = await dbContext.Jednani
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == meetingId);
        deletedMeeting.Should().BeNull();

        var remainingAttendance = await dbContext.Ucast
            .AsNoTracking()
            .Where(item => item.JednaniId == meetingId)
            .ToListAsync();
        remainingAttendance.Should().BeEmpty();
    }
}
