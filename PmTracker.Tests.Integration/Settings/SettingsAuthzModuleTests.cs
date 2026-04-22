using FluentAssertions;
using PmTracker.Tests.Integration.TestInfrastructure;

namespace PmTracker.Tests.Integration.Settings;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class SettingsAuthzModuleTests
{
    private readonly SqlIntegrationFixture _fixture;

    public SettingsAuthzModuleTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BuildNastaveniPanelAsync_ShouldReturnRoleAkcePanel()
    {
        var db = await _fixture.CreateDatabaseAsync("settings_queries_delegate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var queries = IntegrationTestHelper.CreateSettingsAuthzQueries(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var panel = await queries.BuildNastaveniPanelAsync("role-akce", currentUser, userId: db.AdminOsobaId, projektId: null);

        panel.SectionKey.Should().Be("role-akce");
        panel.Role.Should().NotBeEmpty();
        panel.Permissions.Should().NotBeEmpty();
        panel.RolePermissionScopes.Should().NotBeNull();
    }
}
