using PmTracker.Tests.Common;

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

    public Task<TestDatabaseHandle> CreateDatabaseAsync(string prefix, bool includeSeed = true)
    {
        return _databaseManager.CreateInitializedDatabaseAsync(prefix, includeSeed);
    }

    public async Task DisposeAsync()
    {
        await _databaseManager.DisposeAsync();
    }
}
