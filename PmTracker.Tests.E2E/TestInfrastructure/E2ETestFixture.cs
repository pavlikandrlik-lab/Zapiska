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
        command.CommandText = "SELECT TOP (1) id FROM dbo.projekty ORDER BY id";
        var result = await command.ExecuteScalarAsync();

        return Convert.ToInt32(result);
    }
}
