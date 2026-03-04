using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ProjectMembershipDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ProjectMembershipDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AssignProjectSubsystemRole_ShouldAllowSubsystemOnlyMember_AndExposeProjectVisibility()
    {
        var db = await _fixture.CreateDatabaseAsync("subsystem_only_member");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MembershipAdmin");
        var subsystemMemberId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SubsystemOnly");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SUBONLY");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SUBONLY_SYS", adminId);
        var projectSubsystemId = await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        store.AssignProjectSubsystemRole(new AssignProjectSubsystemRoleCommand
        {
            ProjektId = projectId,
            ProjektSubsystemId = projectSubsystemId,
            OsobaId = subsystemMemberId,
            RoleKod = SubsystemRoleCodes.DeputyLead
        }, currentUser);

        var detail = store.BuildProjektDetail(projectId);
        var assignedRow = detail.AktivniRole.Single(row => row.OsobaId == subsystemMemberId);
        assignedRow.AssignmentKind.Should().Be("SUBSYSTEM");
        assignedRow.RoleTypeLabel.Should().Be("Subsystémová");
        assignedRow.SubsystemKod.Should().Be("SUBONLY_SYS");

        var subsystemUserContext = store.BuildCurrentUserContext(subsystemMemberId.ToString(CultureInfo.InvariantCulture));
        subsystemUserContext.VisibleProjectIds.Should().Contain(projectId);
        subsystemUserContext.CanAccessProject(projectId).Should().BeTrue();
    }

    [Fact]
    public async Task AssignProjectRole_ShouldRejectHost_WhenSubsystemRoleExists()
    {
        var db = await _fixture.CreateDatabaseAsync("host_vs_subsystem");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "HostGuardAdmin");
        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "HostGuardMember");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "HOSTSUB");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "HOSTSUB_SYS", adminId);
        var projectSubsystemId = await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        store.AssignProjectSubsystemRole(new AssignProjectSubsystemRoleCommand
        {
            ProjektId = projectId,
            ProjektSubsystemId = projectSubsystemId,
            OsobaId = personId,
            RoleKod = SubsystemRoleCodes.Methodik
        }, currentUser);

        var assignHost = () => store.AssignProjectRole(new AssignProjectRoleCommand
        {
            ProjektId = projectId,
            OsobaId = personId,
            RoleKod = ProjectRoleCodes.Host
        }, currentUser);

        assignHost.Should().Throw<InvalidOperationException>()
            .WithMessage("*rolí v subsystému*Host*");
    }

    [Fact]
    public async Task BuildMeetingParticipantCandidates_ShouldIncludeSubsystemOnlyAndHostOnlyMembers_WithoutDuplicates()
    {
        var db = await _fixture.CreateDatabaseAsync("meeting_candidates_unified");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingCandidatesAdmin");
        var subsystemOnlyId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingSubsystemOnly");
        var hostOnlyId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingHostOnly");
        var mixedMemberId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingMixedMember");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "MEETCAN");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "MEETCAN_SYS", adminId);
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9301);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, hostOnlyId, ProjectRoleCodes.Host);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, mixedMemberId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, subsystemOnlyId, SubsystemRoleCodes.DeputyLead);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, mixedMemberId, SubsystemRoleCodes.Lead);

        var detail = store.BuildJednaniDetail(meetingId);

        detail.AvailableParticipantCandidates.Should().Contain(candidate => candidate.OsobaId == subsystemOnlyId);
        detail.AvailableParticipantCandidates.Should().Contain(candidate => candidate.OsobaId == hostOnlyId);
        detail.AvailableParticipantCandidates.Should().ContainSingle(candidate => candidate.OsobaId == mixedMemberId);
        detail.AvailableParticipantCandidates.Single(candidate => candidate.OsobaId == mixedMemberId)
            .AktivniRole.Count.Should().BeGreaterThanOrEqualTo(2);

        store.AddMeetingParticipant(new AddMeetingParticipantCommand
        {
            ProjektId = projectId,
            JednaniId = meetingId,
            OsobaId = hostOnlyId
        }, currentUser);

        (await dbContext.Ucast.AnyAsync(x => x.JednaniId == meetingId && x.OsobaId == hostOnlyId)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveMeeting_ShouldCreateAttendanceSnapshot_ForSubsystemOnlyMember_AndExcludeHostOnlyMember()
    {
        var db = await _fixture.CreateDatabaseAsync("attendance_snapshot_unified");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttendanceAdmin");
        var subsystemOnlyId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttendanceSubsystemOnly");
        var hostOnlyId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttendanceHostOnly");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "AttendanceOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "ATTSNAP");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "ATTSNAP_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, hostOnlyId, ProjectRoleCodes.Host);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, subsystemOnlyId, SubsystemRoleCodes.DeputyLead);

        var meetingId = store.SaveMeeting(new SaveMeetingCommand
        {
            ProjektId = projectId,
            CisloJednani = 9302,
            DatumPlanovane = new DateTime(2026, 3, 3),
            CasZacatek = new TimeOnly(10, 15),
            Misto = "Zasedačka",
            StavJednani = "OPEN"
        }, currentUser);

        var attendanceIds = await dbContext.Ucast
            .AsNoTracking()
            .Where(x => x.JednaniId == meetingId)
            .Select(x => x.OsobaId)
            .ToListAsync();

        attendanceIds.Should().Contain(ownerId);
        attendanceIds.Should().Contain(subsystemOnlyId);
        attendanceIds.Should().NotContain(hostOnlyId);
    }

    [Fact]
    public async Task ProjectAndMeetingPrintTemplates_ShouldContainUnifiedProjectRoles()
    {
        var db = await _fixture.CreateDatabaseAsync("export_unified_roles");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportOwner");
        var subsystemLeadId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportSubsystemLead");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPROLES");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPROLES_SYS", adminId);
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9303);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, subsystemLeadId, SubsystemRoleCodes.Lead);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportRoleRecord");

        var projectPrint = store.BuildProjectPrintTemplate(projectId, currentUser, autoPrint: false);
        var meetingPrint = store.BuildMeetingPrintTemplate(meetingId, currentUser, autoPrint: false);
        var taskPrint = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);

        projectPrint.ProjektoveRole.Should().Contain(row => row.TypRole == "Projektová" && row.Subsystem == null);
        projectPrint.ProjektoveRole.Should().Contain(row => row.TypRole == "Subsystémová" && row.Subsystem == "EXPROLES_SYS Subsystem");
        meetingPrint.ProjektoveRole.Should().Contain(row => row.TypRole == "Projektová");
        meetingPrint.ProjektoveRole.Should().Contain(row => row.TypRole == "Subsystémová");
        taskPrint.ProjektoveRole.Should().BeEmpty();
    }
}
