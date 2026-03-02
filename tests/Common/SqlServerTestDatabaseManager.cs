using System.Globalization;
using Microsoft.Data.SqlClient;
using DotNet.Testcontainers.Builders;
using Testcontainers.MsSql;

namespace PmTracker.Tests.Common;

public sealed class SqlServerTestDatabaseManager : IAsyncDisposable
{
    private readonly MsSqlContainer _container;
    private bool _started;

    public SqlServerTestDatabaseManager()
    {
        _container = new MsSqlBuilder("mcr.microsoft.com/azure-sql-edge:latest")
            .WithPassword("PmTracker!Test2026")
            // MsSqlBuilder default readiness expects sqlcmd in image. Azure SQL Edge image does not include it.
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1433))
            .Build();
    }

    public string MasterConnectionString => _container.GetConnectionString();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            return;
        }

        await _container.StartAsync(cancellationToken);
        await WaitForSqlReadyAsync(cancellationToken);
        _started = true;
    }

    public async Task<TestDatabaseHandle> CreateInitializedDatabaseAsync(string databasePrefix, bool includeSeed, CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            await StartAsync(cancellationToken);
        }

        var databaseName = BuildDatabaseName(databasePrefix);
        await CreateDatabaseAsync(databaseName, cancellationToken);

        var builder = new SqlConnectionStringBuilder(MasterConnectionString)
        {
            InitialCatalog = databaseName,
            Encrypt = false,
            TrustServerCertificate = true
        };

        var dbConnectionString = builder.ConnectionString;
        var scripts = RepositoryPaths.GetBootstrapScripts(includeSeed);
        await SqlScriptRunner.ExecuteScriptsAsync(dbConnectionString, scripts, cancellationToken);

        var adminOsobaId = await ReadAdminOsobaIdAsync(dbConnectionString, cancellationToken);
        return new TestDatabaseHandle(databaseName, dbConnectionString, adminOsobaId);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task CreateDatabaseAsync(string databaseName, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        command.CommandTimeout = 60;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ReadAdminOsobaIdAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP (1) id FROM dbo.osoby ORDER BY id";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static string BuildDatabaseName(string prefix)
    {
        var normalizedPrefix = string.Concat((prefix ?? "pmtracker_test").Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
        if (string.IsNullOrWhiteSpace(normalizedPrefix))
        {
            normalizedPrefix = "pmtracker_test";
        }

        var unique = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N")[..8];
        var dbName = $"{normalizedPrefix}_{unique}";

        if (dbName.Length > 120)
        {
            dbName = dbName[..120];
        }

        return dbName;
    }

    private async Task WaitForSqlReadyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = new SqlConnection(MasterConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is SqlException || ex is InvalidOperationException)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new TimeoutException("SQL test container did not become ready in time.", lastError);
    }

}

public sealed record TestDatabaseHandle(string DatabaseName, string ConnectionString, int AdminOsobaId);
