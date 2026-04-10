using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ProjectRoleImplicitPermissionGrantDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ProjectRoleImplicitPermissionGrantDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BuildCurrentUserContext_ShouldIncludeProjectAdminImplicitGrants()
    {
        var db = await _fixture.CreateDatabaseAsync("proj_admin_grants");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var osobaId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "projadmin");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRJADM");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, osobaId, ProjectRoleCodes.ProjectAdmin);

        var currentUser = store.BuildCurrentUserContext(osobaId.ToString(CultureInfo.InvariantCulture));

        currentUser.VisibleProjectIds.Should().Contain(projectId);
        currentUser.HasPermission(PermissionKeys.TeamManage, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.RecordsEdit, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.MeetingsCreate, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.MeetingsEdit, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.ProjectsEdit, projectId).Should().BeFalse();

        currentUser.PermissionGrants.Should().Contain(x =>
            x.PermissionKey == PermissionKeys.TeamManage &&
            x.ScopeLevel == "PROJECT" &&
            x.ScopeMode == "INCLUDE" &&
            x.IsAllowed &&
            x.ProjectIds.SequenceEqual(new[] { projectId }));
    }

    [Fact]
    public async Task BuildCurrentUserContext_ShouldIncludeProjectManagerImplicitGrants()
    {
        var db = await _fixture.CreateDatabaseAsync("proj_manager_grants");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var osobaId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "projmanager");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRJMGR");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, osobaId, ProjectRoleCodes.ProjectManager);

        var currentUser = store.BuildCurrentUserContext(osobaId.ToString(CultureInfo.InvariantCulture));

        currentUser.VisibleProjectIds.Should().Contain(projectId);
        currentUser.HasPermission(PermissionKeys.TeamManage, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.RecordsEdit, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.MeetingsCreate, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.MeetingsEdit, projectId).Should().BeTrue();
        currentUser.HasPermission(PermissionKeys.ProjectsEdit, projectId).Should().BeFalse();

        currentUser.PermissionGrants.Should().Contain(x =>
            x.PermissionKey == PermissionKeys.TeamManage &&
            x.ScopeLevel == "PROJECT" &&
            x.ScopeMode == "INCLUDE" &&
            x.IsAllowed &&
            x.ProjectIds.SequenceEqual(new[] { projectId }));
    }

    [Fact]
    public async Task BuildCurrentUserContext_ShouldIncludeSubsystemDeputyImplicitCommentGrant_AndProfileDerivedRights()
    {
        var db = await _fixture.CreateDatabaseAsync("subsystem_deputy_grants");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var osobaId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "subsdeputy");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SUBDEP");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SUBDEP_SYS", osobaId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, osobaId, SubsystemRoleCodes.DeputyLead);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, osobaId, subsystemId, "U", "DeputyImplicitComment");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "DRAFT", meetingNumber: 9300);

        var currentUser = store.BuildCurrentUserContext(osobaId.ToString(CultureInfo.InvariantCulture));
        var profile = store.BuildProfilPage(currentUser, projektId: projectId);

        currentUser.VisibleProjectIds.Should().Contain(projectId);
        currentUser.HasPermission(PermissionKeys.RecordsCommentSubsystemLead, projectId).Should().BeTrue();
        currentUser.PermissionGrants.Should().Contain(x =>
            x.PermissionKey == PermissionKeys.RecordsCommentSubsystemLead &&
            x.ScopeLevel == "PROJECT" &&
            x.ScopeMode == "INCLUDE" &&
            x.IsAllowed &&
            x.ProjectIds.SequenceEqual(new[] { projectId }) &&
            x.SourceType == "SUBSYSTEM_ROLE" &&
            x.SourceRoleCode == SubsystemRoleCodes.DeputyLead &&
            x.SourceProjectId == projectId);

        profile.OdvozenaPrava.Should().Contain(x =>
            x.PermissionKlic == PermissionKeys.RecordsCommentSubsystemLead &&
            x.IsAllowed &&
            x.SourceSummary.Contains("Subsystémová role", StringComparison.OrdinalIgnoreCase));

        store.AddComment(new AddCommentCommand
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            Text = "Deputy lead implicit permission comment"
        }, currentUser);

        (await dbContext.Vyjadreni.AsNoTracking().AnyAsync(x =>
            x.ZaznamId == recordId &&
            x.JednaniId == meetingId &&
            x.AutorOsobaId == osobaId))
            .Should()
            .BeTrue();
    }
}
