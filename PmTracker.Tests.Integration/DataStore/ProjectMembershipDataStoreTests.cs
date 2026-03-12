using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
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

    [Fact]
    public async Task TaskPrintTemplate_ShouldOrderCommentsAscending()
    {
        var db = await _fixture.CreateDatabaseAsync("export_comment_sort_direction");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportSortAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportSortOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPSORT");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPSORT_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportSortRecord");
        var meeting1Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9401);
        var meeting2Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9402);
        var meeting3Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9403);

        dbContext.Vyjadreni.AddRange(
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting2Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Meeting 2 comment",
                DatumVyjadreni = new DateTime(2026, 6, 2, 9, 0, 0)
            },
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting1Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Meeting 1 comment",
                DatumVyjadreni = new DateTime(2026, 6, 1, 9, 0, 0)
            },
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting3Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Meeting 3 comment",
                DatumVyjadreni = new DateTime(2026, 6, 3, 9, 0, 0)
            });
        await dbContext.SaveChangesAsync();

        var model = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);

        model.Zaznamy.Should().ContainSingle();
        var meetingNumbers = model.Zaznamy.Single().Vyjadreni.Select(x => x.JednaniCislo).ToList();
        meetingNumbers.Should().Equal(9401, 9402, 9403);
    }

    [Fact]
    public async Task ProjectAndMeetingPrintTemplates_ShouldOrderCommentsAscending()
    {
        var db = await _fixture.CreateDatabaseAsync("export_comment_sort_direction_all_templates");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportSortAllAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportSortAllOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPSORTALL");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPSORTALL_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportSortAllRecord");
        var meeting1Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9501);
        var meeting2Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9502);
        var meeting3Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9503);

        dbContext.Vyjadreni.AddRange(
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting2Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Meeting 2 comment",
                DatumVyjadreni = new DateTime(2026, 7, 2, 9, 0, 0)
            },
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting1Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Meeting 1 comment",
                DatumVyjadreni = new DateTime(2026, 7, 1, 9, 0, 0)
            },
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting3Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Meeting 3 comment",
                DatumVyjadreni = new DateTime(2026, 7, 3, 9, 0, 0)
            });
        await dbContext.SaveChangesAsync();

        var projectModel = store.BuildProjectPrintTemplate(projectId, currentUser, autoPrint: false);
        var meetingModel = store.BuildMeetingPrintTemplate(meeting3Id, currentUser, autoPrint: false);

        projectModel.Zaznamy.Should().ContainSingle();
        meetingModel.Zaznamy.Should().ContainSingle();

        var projectMeetingNumbers = projectModel.Zaznamy.Single().Vyjadreni.Select(x => x.JednaniCislo).ToList();
        var meetingMeetingNumbers = meetingModel.Zaznamy.Single().Vyjadreni.Select(x => x.JednaniCislo).ToList();

        projectMeetingNumbers.Should().Equal(9501, 9502, 9503);
        meetingMeetingNumbers.Should().Equal(9501, 9502, 9503);
    }

    [Fact]
    public async Task TaskPrintTemplate_ShouldKeepNewestCommentsWhenCommentLimitApplies()
    {
        var db = await _fixture.CreateDatabaseAsync("export_comment_limit_newest");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLimitAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLimitOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPLIMIT");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPLIMIT_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportLimitRecord");

        for (var index = 1; index <= 12; index++)
        {
            var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9700 + index);
            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = $"Meeting {index} comment",
                DatumVyjadreni = new DateTime(2026, 8, index, 9, 0, 0)
            });
        }

        await dbContext.SaveChangesAsync();

        var model = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);
        model.Zaznamy.Should().ContainSingle();

        var meetingNumbers = model.Zaznamy.Single().Vyjadreni
            .Select(x => x.JednaniCislo)
            .ToList();

        meetingNumbers.Should().Equal(9703, 9704, 9705, 9706, 9707, 9708, 9709, 9710, 9711, 9712);
    }

    [Fact]
    public async Task TaskPrintTemplate_ShouldUseLineBudgetForCommentLimit()
    {
        var db = await _fixture.CreateDatabaseAsync("export_comment_limit_line_budget");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLineBudgetAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLineBudgetOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPLINE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPLINE_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportLineBudgetRecord");

        var longBody = new string('X', 600);
        for (var index = 1; index <= 6; index++)
        {
            var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9800 + index);
            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = longBody,
                DatumVyjadreni = new DateTime(2026, 9, index, 9, 0, 0)
            });
        }

        await dbContext.SaveChangesAsync();

        var model = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);
        model.Zaznamy.Should().ContainSingle();

        var meetingNumbers = model.Zaznamy.Single().Vyjadreni
            .Select(x => x.JednaniCislo)
            .ToList();

        meetingNumbers.Should().Equal(9805, 9806);
    }

    [Fact]
    public async Task TaskPrintTemplate_ShouldKeepAtLeastNewestComment_WhenItExceedsLineBudget()
    {
        var db = await _fixture.CreateDatabaseAsync("export_comment_limit_line_budget_keep_newest");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLineBudgetNewestAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLineBudgetNewestOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPLINELAST");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPLINELAST_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportLineBudgetNewestRecord");

        var meeting1Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9811);
        var meeting2Id = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9812);

        dbContext.Vyjadreni.AddRange(
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting1Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Older short comment",
                DatumVyjadreni = new DateTime(2026, 9, 11, 9, 0, 0)
            },
            new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meeting2Id,
                AutorOsobaId = ownerId,
                TextVyjadreni = new string('Y', 5000),
                DatumVyjadreni = new DateTime(2026, 9, 12, 9, 0, 0)
            });

        await dbContext.SaveChangesAsync();

        var model = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);
        model.Zaznamy.Should().ContainSingle();

        var meetingNumbers = model.Zaznamy.Single().Vyjadreni
            .Select(x => x.JednaniCislo)
            .ToList();

        meetingNumbers.Should().Equal(9812);
    }

    [Fact]
    public async Task TaskPrintTemplate_ShouldCountExplicitLineBreaksInCommentBudget()
    {
        var db = await _fixture.CreateDatabaseAsync("export_comment_limit_line_breaks");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLineBreakAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportLineBreakOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPLINEBREAK");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPLINEBREAK_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportLineBreakRecord");

        var multilineBody = string.Join('\n', Enumerable.Repeat("line", 8));
        for (var index = 1; index <= 4; index++)
        {
            var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9820 + index);
            dbContext.Vyjadreni.Add(new VyjadreniEntity
            {
                ZaznamId = recordId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = multilineBody,
                DatumVyjadreni = new DateTime(2026, 9, 20 + index, 9, 0, 0)
            });
        }

        await dbContext.SaveChangesAsync();

        var model = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);
        model.Zaznamy.Should().ContainSingle();

        var meetingNumbers = model.Zaznamy.Single().Vyjadreni
            .Select(x => x.JednaniCislo)
            .ToList();

        meetingNumbers.Should().Equal(9823, 9824);
    }

    [Fact]
    public async Task TaskPrintTemplate_ShouldIncludePlannedDeliveryDateOnlyForExternalLinksThatHaveIt()
    {
        var db = await _fixture.CreateDatabaseAsync("export_external_planned_delivery");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportExternalAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportExternalOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPEXT");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPEXT_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportExternalRecord");
        await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9601);

        var pmpTypeId = await dbContext.CiselnikTypuExternichOdkazu
            .Where(x => x.Kod == "PMP")
            .Select(x => x.Id)
            .FirstAsync();
        var nesTypeId = await dbContext.CiselnikTypuExternichOdkazu
            .Where(x => x.Kod == "NES")
            .Select(x => x.Id)
            .FirstAsync();

        dbContext.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = pmpTypeId,
                Cislo = "PMP-123",
                PlanDodani = new DateTime(2026, 4, 15)
            },
            new ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = nesTypeId,
                Cislo = "NES-456",
                PlanDodani = null
            });
        await dbContext.SaveChangesAsync();

        var model = store.BuildTaskPrintTemplate(projectId, recordId, currentUser, autoPrint: false);
        model.Zaznamy.Should().ContainSingle();

        var externalLinks = model.Zaznamy.Single().ExterniVazby;
        externalLinks.Should().Contain("PMP PMP-123 (plán dodání: 15.04.2026)");
        externalLinks.Should().Contain("NES NES-456");
        externalLinks.Should().NotContain(link => link.Contains("NES NES-456 (", StringComparison.Ordinal));
    }
}
