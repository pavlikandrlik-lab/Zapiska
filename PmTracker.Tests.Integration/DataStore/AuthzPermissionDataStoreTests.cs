using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class AuthzPermissionDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public AuthzPermissionDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveAuthzPermission_ShouldPersist_WhenUsingValidValues()
    {
        var db = await _fixture.CreateDatabaseAsync("perm_valid");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var categoryId = await dbContext.AuthzPermissionCategories
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .FirstAsync();

        var existingKeys = await dbContext.AuthzPermissions
            .Select(x => x.Klic)
            .ToListAsync();

        var selectedKey = PermissionKeys.BuildLookupOptions()
            .Select(x => x.Value)
            .FirstOrDefault(key => existingKeys.All(existing => !string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)));

        if (selectedKey is null)
        {
            var existingPermission = await dbContext.AuthzPermissions
                .OrderBy(x => x.Id)
                .FirstAsync();

            var updatedName = existingPermission.Nazev + " (test update)";
            store.SaveAuthzPermission(new SaveAuthzPermissionCommand
            {
                Id = existingPermission.Id,
                Klic = existingPermission.Klic,
                Nazev = updatedName,
                CategoryId = categoryId,
                ScopeLevel = "PROJECT"
            }, currentUser);

            var reloaded = await dbContext.AuthzPermissions.FirstAsync(x => x.Id == existingPermission.Id);
            reloaded.Nazev.Should().Be(updatedName);
            return;
        }

        var command = new SaveAuthzPermissionCommand
        {
            Klic = selectedKey,
            Nazev = "Test permission",
            CategoryId = categoryId,
            ScopeLevel = "PROJECT"
        };

        store.SaveAuthzPermission(command, currentUser);

        var saved = await dbContext.AuthzPermissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Klic == selectedKey);

        saved.Should().NotBeNull();
        saved!.Nazev.Should().Be("Test permission");
        saved.ScopeLevel.Should().Be("PROJECT");
    }

    [Fact]
    public async Task SaveAuthzPermission_ShouldRejectUnsupportedKey()
    {
        var db = await _fixture.CreateDatabaseAsync("perm_badkey");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);
        var categoryId = await dbContext.AuthzPermissionCategories.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();

        var act = () => store.SaveAuthzPermission(new SaveAuthzPermissionCommand
        {
            Klic = "unsupported.key",
            Nazev = "Unsupported",
            CategoryId = categoryId,
            ScopeLevel = "PROJECT"
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*není v seznamu podporovaných akcí*");
    }

    [Fact]
    public async Task SaveAuthzPermission_ShouldRejectDuplicateKey()
    {
        var db = await _fixture.CreateDatabaseAsync("perm_duplicate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var existing = await dbContext.AuthzPermissions.AsNoTracking().OrderBy(x => x.Id).FirstAsync();

        var act = () => store.SaveAuthzPermission(new SaveAuthzPermissionCommand
        {
            Klic = existing.Klic,
            Nazev = existing.Nazev + " duplicate",
            CategoryId = existing.CategoryId,
            ScopeLevel = existing.ScopeLevel
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*už existuje*");
    }

    [Fact]
    public async Task SaveAuthzPermission_ShouldRejectUnknownCategory()
    {
        var db = await _fixture.CreateDatabaseAsync("perm_badcat");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var supportedKey = PermissionKeys.BuildLookupOptions().First().Value;
        var unknownCategoryId = (await dbContext.AuthzPermissionCategories.MaxAsync(x => (int?)x.Id) ?? 0) + 999;

        var act = () => store.SaveAuthzPermission(new SaveAuthzPermissionCommand
        {
            Klic = supportedKey,
            Nazev = "Invalid category",
            CategoryId = unknownCategoryId,
            ScopeLevel = "PROJECT"
        }, currentUser);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*kategorie akcí neexistuje*");
    }
}
