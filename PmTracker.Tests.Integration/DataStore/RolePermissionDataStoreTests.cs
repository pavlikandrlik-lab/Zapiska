using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RolePermissionDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RolePermissionDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveRolePermission_ShouldRejectIncludeWithUnknownProject()
    {
        var db = await _fixture.CreateDatabaseAsync("roleperm_invalid_project");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var roleId = await CreateCustomRoleAsync(dbContext, "RLPINV");
        var permissionId = await dbContext.AuthzPermissions.Select(x => x.Id).FirstAsync();

        var act = () => store.SaveRolePermission(new SaveRolePermissionCommand
        {
            RoleId = roleId,
            PermissionId = permissionId,
            ScopeMode = "INCLUDE",
            IsAllowed = true,
            ProjektIds = new List<int> { 987654 }
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*projekty pro INCLUDE mapování neexistují*");
    }

    [Fact]
    public async Task SaveRolePermission_ShouldPersistIncludeProjects_WhenValid()
    {
        var db = await _fixture.CreateDatabaseAsync("roleperm_valid");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var roleId = await CreateCustomRoleAsync(dbContext, "RLPVAL");
        var permissionId = await dbContext.AuthzPermissions.Select(x => x.Id).FirstAsync();
        var projectIds = await dbContext.Projekty.OrderBy(x => x.Id).Select(x => x.Id).Take(2).ToListAsync();

        projectIds.Should().NotBeEmpty();

        store.SaveRolePermission(new SaveRolePermissionCommand
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

    [Fact]
    public async Task DeleteRolePermission_ShouldRemoveIncludeProjectLinks()
    {
        var db = await _fixture.CreateDatabaseAsync("roleperm_delete_include");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var roleId = await CreateCustomRoleAsync(dbContext, "RLPDEL");
        var permissionId = await dbContext.AuthzPermissions.Select(x => x.Id).FirstAsync();
        var projectIds = await dbContext.Projekty.OrderBy(x => x.Id).Select(x => x.Id).Take(2).ToListAsync();
        projectIds.Should().NotBeEmpty();

        store.SaveRolePermission(new SaveRolePermissionCommand
        {
            RoleId = roleId,
            PermissionId = permissionId,
            ScopeMode = "INCLUDE",
            IsAllowed = true,
            ProjektIds = projectIds
        }, currentUser);

        var mappingId = await dbContext.AuthzRolePermissions
            .AsNoTracking()
            .Where(x => x.RoleId == roleId && x.PermissionId == permissionId)
            .Select(x => x.Id)
            .SingleAsync();

        store.DeleteRolePermission(new DeleteRolePermissionCommand
        {
            Id = mappingId
        }, currentUser);

        (await dbContext.AuthzRolePermissions.AsNoTracking().AnyAsync(x => x.Id == mappingId)).Should().BeFalse();
        (await dbContext.AuthzRolePermissionProjects.AsNoTracking().AnyAsync(x => x.RolePermissionId == mappingId)).Should().BeFalse();
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
