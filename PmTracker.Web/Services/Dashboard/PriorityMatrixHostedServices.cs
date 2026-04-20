/// <summary>
/// Hosted services pro prioritní matici: bootstrap při startu, nightly rebuild a queue-driven rebuild.
/// Závisí na IPriorityMatrixRebuildService, PriorityMatrixRebuildService a IPriorityMatrixRebuildQueue.
/// </summary>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PmTracker.Web.Data;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Dashboard;

internal sealed class PriorityMatrixBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DashboardPriorityOptions _options;
    private readonly ILogger<PriorityMatrixBootstrapHostedService> _logger;

    public PriorityMatrixBootstrapHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DashboardPriorityOptions> options,
        ILogger<PriorityMatrixBootstrapHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_options.BootstrapFullRebuildOnStartup)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var state = await dbContext.ZaznamPriorityRebuildState
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, ct);
        if (state is not null
            && !string.Equals(state.LastFullRebuildStatus, PriorityMatrixRebuildStatuses.Never, StringComparison.OrdinalIgnoreCase)
            && state.LastFullRebuildAt.HasValue)
        {
            return;
        }

        _logger.LogInformation("Priority matrix bootstrap full rebuild started.");
        await scope.ServiceProvider.GetRequiredService<IPriorityMatrixRebuildService>().FullRebuildAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class PriorityMatrixNightlyRebuildHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DashboardPriorityOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PriorityMatrixNightlyRebuildHostedService> _logger;

    public PriorityMatrixNightlyRebuildHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DashboardPriorityOptions> options,
        TimeProvider timeProvider,
        ILogger<PriorityMatrixNightlyRebuildHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nightlyTime = _options.GetNightlyRebuildTime();

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetLocalNow();
            var nextRun = new DateTimeOffset(
                now.Year,
                now.Month,
                now.Day,
                nightlyTime.Hour,
                nightlyTime.Minute,
                0,
                now.Offset);
            if (nextRun <= now)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IPriorityMatrixRebuildService>().FullRebuildAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nightly priority matrix rebuild failed.");
            }
        }
    }
}

internal sealed class PriorityMatrixQueuedRebuildHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPriorityMatrixRebuildQueue _queue;
    private readonly ILogger<PriorityMatrixQueuedRebuildHostedService> _logger;

    public PriorityMatrixQueuedRebuildHostedService(
        IServiceScopeFactory scopeFactory,
        IPriorityMatrixRebuildQueue queue,
        ILogger<PriorityMatrixQueuedRebuildHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var subsystemId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<PriorityMatrixRebuildService>();
                await service.RebuildForSubsystemAsync(subsystemId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued priority matrix rebuild failed for subsystem {SubsystemId}.", subsystemId);
            }
            finally
            {
                _queue.MarkCompleted(subsystemId);
            }
        }
    }
}
