using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Settings;

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

    [Fact]
    public async Task SaveRolePermissionAsync_ShouldPersistIncludeProjects_WhenCalledViaSettingsService()
    {
        var db = await _fixture.CreateDatabaseAsync("settings_commands_roleperm");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var settingsService = new SettingsService(
            IntegrationTestHelper.CreateSettingsAuthzQueries(dbContext),
            IntegrationTestHelper.CreateSettingsAuthzCommands(dbContext));
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var roleId = await CreateCustomRoleAsync(dbContext, "STMOD");
        var permissionId = await dbContext.AuthzPermissions.Select(x => x.Id).FirstAsync();
        var projectIds = await dbContext.Projekty.OrderBy(x => x.Id).Select(x => x.Id).Take(2).ToListAsync();

        projectIds.Should().NotBeEmpty();

        await settingsService.SaveRolePermissionAsync(new SaveRolePermissionCommand
        {
            RoleId = roleId,
            PermissionId = permissionId,
            ScopeMode = "INCLUDE",
            IsAllowed = true,
            ProjektIds = projectIds
        }, currentUser);

        var rolePermission = await dbContext.AuthzRolePermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId);

        rolePermission.Should().NotBeNull();
        rolePermission!.ScopeMode.Should().Be("INCLUDE");
        rolePermission.IsAllowed.Should().BeTrue();

        var savedProjectIds = await dbContext.AuthzRolePermissionProjects
            .AsNoTracking()
            .Where(x => x.RolePermissionId == rolePermission.Id)
            .Select(x => x.ProjektId)
            .OrderBy(x => x)
            .ToListAsync();

        savedProjectIds.Should().BeEquivalentTo(projectIds);
    }

    private static async Task<int> CreateCustomRoleAsync(DbContext dbContext, string codePrefix)
    {
        var role = new AuthzRoleEntity
        {
            Kod = $"{codePrefix}_{Guid.NewGuid():N}"[..20],
            Nazev = "Test role",
            Popis = "Test role",
            IsSystem = false,
            IsActive = true
        };

        dbContext.Set<AuthzRoleEntity>().Add(role);
        await dbContext.SaveChangesAsync();
        return role.Id;
    }
}
