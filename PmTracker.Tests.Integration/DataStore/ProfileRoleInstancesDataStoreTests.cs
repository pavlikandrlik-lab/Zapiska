using FluentAssertions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>A5 (2026-07-08): profil skládá karty per instance role (Typ/Role/Projekt/Subsystém).</summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ProfileRoleInstancesDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;
    public ProfileRoleInstancesDataStoreTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task BuildProfilPage_GroupsRolesPerInstance_WithProjectAndSubsystem()
    {
        var db = await _fixture.CreateDatabaseAsync("profile_role_instances");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProfInstAdmin");
        var personId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProfInstUser");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PROFINST");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "PROFINST_SYS", adminId);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, personId, ProjectRoleCodes.ProjectManager);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, personId, SubsystemRoleCodes.Lead);

        var user = IntegrationTestHelper.BuildUser(personId, isSuperAdmin: false, visibleProjectIds: [projectId]);
        var page = store.BuildProfilPage(user, projektId: null);

        page.MojeRole.Should().Contain(x => x.TypRole == "Projektová" && x.ProjektZkratka == "PROFINST");
        var sub = page.MojeRole.SingleOrDefault(x => x.TypRole == "Subsystémová");
        sub.Should().NotBeNull();
        sub!.ProjektZkratka.Should().Be("PROFINST");
        sub.SubsystemKod.Should().Be("PROFINST_SYS");
        sub.Akce.Should().NotBeEmpty("instance nese akce z AuthzRolePermissions napojené role");
    }
}
