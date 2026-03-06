using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using PmTracker.Tests.Common;

namespace PmTracker.Tests.E2E.TestInfrastructure;

[CollectionDefinition(CollectionName)]
public sealed class E2ECollection : ICollectionFixture<E2ETestFixture>
{
    public const string CollectionName = "e2e-environment";
}

public sealed class E2ETestFixture : IAsyncLifetime
{
    private readonly SqlServerTestDatabaseManager _databaseManager = new();
    private Process? _webProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public TestDatabaseHandle Database { get; private set; } = null!;
    public int ProjectId { get; private set; }
    public int AdminOsobaId => Database.AdminOsobaId;
    public string BaseUrl { get; private set; } = "http://127.0.0.1:5188";

    public async Task InitializeAsync()
    {
        await _databaseManager.StartAsync();
        Database = await _databaseManager.CreateInitializedDatabaseAsync("e2e", includeSeed: true);
        ProjectId = await ResolveFirstProjectIdAsync(Database.ConnectionString);
        await EnsureProjectHasEditorPrerequisitesAsync(Database.ConnectionString, ProjectId, Database.AdminOsobaId);

        await StartWebApplicationAsync();
        await WaitForWebReadinessAsync();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !IsHeadedModeEnabled()
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_webProcess is not null && !_webProcess.HasExited)
        {
            try
            {
                _webProcess.Kill(entireProcessTree: true);
                await _webProcess.WaitForExitAsync();
            }
            catch
            {
                // No-op during cleanup.
            }
        }

        await _databaseManager.DisposeAsync();
    }

    public async Task<IPage> NewPageAsync()
    {
        if (_browser is null)
        {
            throw new InvalidOperationException("Browser není inicializovaný.");
        }

        var context = await _browser.NewContextAsync();
        return await context.NewPageAsync();
    }

    private async Task StartWebApplicationAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{RepositoryPaths.WebProjectPath}\" --urls {BaseUrl} --no-launch-profile",
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ConnectionStrings__PmTrackerDb"] = Database.ConnectionString;
        startInfo.Environment["PmTracker__Data__Provider"] = "SqlServer";
        startInfo.Environment["PmTracker__Data__SqlServer__ConnectionStringName"] = "PmTrackerDb";
        startInfo.Environment["PmTracker__Data__SqlServer__CommandTimeoutSeconds"] = "60";

        _webProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Nepodařilo se spustit web aplikaci pro E2E testy.");
    }

    private async Task WaitForWebReadinessAsync()
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var deadline = DateTime.UtcNow.AddSeconds(90);
        var url = $"{BaseUrl}/Projekty?asUser={AdminOsobaId}";

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
                // Keep retrying until deadline.
            }

            if (_webProcess is { HasExited: true })
            {
                var stdout = await _webProcess.StandardOutput.ReadToEndAsync();
                var stderr = await _webProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"E2E web proces skončil předčasně.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("E2E web aplikace nenaběhla do 90 sekund.");
    }

    private static bool IsHeadedModeEnabled()
    {
        var value = Environment.GetEnvironmentVariable("E2E_HEADED");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> ResolveFirstProjectIdAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) p.id
            FROM dbo.projekty p
            WHERE EXISTS (
                SELECT 1
                FROM dbo.projekt_subsystemy ps
                WHERE ps.projekt_id = p.id
                  AND ps.datum_odebrani IS NULL
            )
            ORDER BY p.id;
            """;
        var result = await command.ExecuteScalarAsync();

        if (result is null || result == DBNull.Value)
        {
            await using var fallbackCommand = connection.CreateCommand();
            fallbackCommand.CommandText = "SELECT TOP (1) id FROM dbo.projekty ORDER BY id";
            var fallbackResult = await fallbackCommand.ExecuteScalarAsync();
            return Convert.ToInt32(fallbackResult);
        }

        return Convert.ToInt32(result);
    }

    private static async Task EnsureProjectHasEditorPrerequisitesAsync(string connectionString, int projectId, int defaultOsobaId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var hasActiveSubsystem = false;
        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = """
                SELECT TOP (1) 1
                FROM dbo.projekt_subsystemy
                WHERE projekt_id = @projectId
                  AND datum_odebrani IS NULL;
                """;
            existsCommand.Parameters.AddWithValue("@projectId", projectId);
            var subsystemExists = await existsCommand.ExecuteScalarAsync();
            hasActiveSubsystem = subsystemExists is not null && subsystemExists != DBNull.Value;
        }

        if (!hasActiveSubsystem)
        {
            int subsystemId;
            await using (var existingSubsystemCommand = connection.CreateCommand())
            {
                existingSubsystemCommand.CommandText = "SELECT TOP (1) id FROM dbo.subsystemy ORDER BY id;";
                var existingSubsystem = await existingSubsystemCommand.ExecuteScalarAsync();
                if (existingSubsystem is not null && existingSubsystem != DBNull.Value)
                {
                    subsystemId = Convert.ToInt32(existingSubsystem);
                }
                else
                {
                    var subsystemCode = $"E2E_SUB_{projectId}";
                    var subsystemName = $"E2E Subsystém {projectId}";
                    await using var insertSubsystemCommand = connection.CreateCommand();
                    insertSubsystemCommand.CommandText = """
                        INSERT INTO dbo.subsystemy ([kód], nazev)
                        VALUES (@kod, @nazev);
                        SELECT CAST(SCOPE_IDENTITY() AS int);
                        """;
                    insertSubsystemCommand.Parameters.AddWithValue("@kod", subsystemCode);
                    insertSubsystemCommand.Parameters.AddWithValue("@nazev", subsystemName);
                    subsystemId = Convert.ToInt32(await insertSubsystemCommand.ExecuteScalarAsync());
                }
            }

            await using var insertMappingCommand = connection.CreateCommand();
            insertMappingCommand.CommandText = """
                INSERT INTO dbo.projekt_subsystemy (projekt_id, subsystem_id)
                VALUES (@projectId, @subsystemId);
                """;
            insertMappingCommand.Parameters.AddWithValue("@projectId", projectId);
            insertMappingCommand.Parameters.AddWithValue("@subsystemId", subsystemId);
            await insertMappingCommand.ExecuteNonQueryAsync();
        }

        await using var ownerExistsCommand = connection.CreateCommand();
        ownerExistsCommand.CommandText = """
            SELECT TOP (1) 1
            FROM dbo.obsazeni_projektu
            WHERE projekt_id = @projectId
              AND datum_odebrani IS NULL;
            """;
        ownerExistsCommand.Parameters.AddWithValue("@projectId", projectId);
        var hasActiveOwner = await ownerExistsCommand.ExecuteScalarAsync();
        if (hasActiveOwner is not null && hasActiveOwner != DBNull.Value)
        {
            return;
        }

        await using var roleCommand = connection.CreateCommand();
        roleCommand.CommandText = """
            SELECT TOP (1) id
            FROM dbo.ciselnik_roli_projektu
            ORDER BY CASE WHEN kod = N'HOST' THEN 0 ELSE 1 END, id;
            """;
        var roleId = Convert.ToInt32(await roleCommand.ExecuteScalarAsync());

        await using var insertOwnerCommand = connection.CreateCommand();
        insertOwnerCommand.CommandText = """
            INSERT INTO dbo.obsazeni_projektu (projekt_id, osoba_id, role_id)
            VALUES (@projectId, @osobaId, @roleId);
            """;
        insertOwnerCommand.Parameters.AddWithValue("@projectId", projectId);
        insertOwnerCommand.Parameters.AddWithValue("@osobaId", defaultOsobaId);
        insertOwnerCommand.Parameters.AddWithValue("@roleId", roleId);
        await insertOwnerCommand.ExecuteNonQueryAsync();
    }
}
