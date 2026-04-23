using PmTracker.Tests.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Integration.TestInfrastructure;

[CollectionDefinition(CollectionName)]
public sealed class SqlIntegrationCollection : ICollectionFixture<SqlIntegrationFixture>
{
    public const string CollectionName = "sql-integration";
}

public sealed class SqlIntegrationFixture : IAsyncLifetime
{
    private readonly SqlServerTestDatabaseManager _databaseManager = new();

    public Task InitializeAsync()
    {
        return _databaseManager.StartAsync();
    }

    public async Task<TestDatabaseHandle> CreateDatabaseAsync(string prefix, bool includeSeed = true)
    {
        var handle = await _databaseManager.CreateInitializedDatabaseAsync(prefix, includeSeed);

        // F8 A1 fix 2026-04-23: bootstrap SQL (db_rules_schema.sql) obsahuje jen minimální
        // baseline permission katalog (SUPERADMIN/APP_ADMIN core). Produkce doseeduje plný
        // 76-klíčový per-action katalog + RoleMappings aplikačním PermissionSeederem při
        // Program.cs startu. Integration testy neprocházejí Program.cs, takže musíme
        // PermissionSeeder spustit explicitně — jinak ADM_PROJ/PROJ_MAN/VEDOUCI_SUBSYSTEMU
        // authz role v DB neexistují a implicit grants přes ObsazeniProjektu se nezresolvují.
        await using var dbContext = IntegrationTestHelper.CreateDbContext(handle.ConnectionString);
        var seeder = new PermissionSeeder(dbContext);
        await seeder.SeedAsync();

        return handle;
    }

    public async Task DisposeAsync()
    {
        await _databaseManager.DisposeAsync();
    }
}
