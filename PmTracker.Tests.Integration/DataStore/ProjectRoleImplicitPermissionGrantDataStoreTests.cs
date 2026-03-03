using System.Globalization;
using FluentAssertions;
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
}
