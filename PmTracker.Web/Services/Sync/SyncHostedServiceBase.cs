using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PmTracker.Web.Services.Sync;

public abstract class SyncHostedServiceBase<TSettings> : BackgroundService
    where TSettings : class, ISyncJobSettings
{
    private static readonly TimeSpan DisabledCheckInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StaleRunGuard = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISyncJobRunLock<TSettings> _runLock;
    private readonly ManualTriggerSignal<TSettings> _manualSignal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    protected SyncHostedServiceBase(
        IServiceScopeFactory scopeFactory,
        ISyncJobRunLock<TSettings> runLock,
        ManualTriggerSignal<TSettings> manualSignal,
        TimeProvider timeProvider,
        ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _runLock = runLock;
        _manualSignal = manualSignal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected abstract string JobKey { get; }
    protected abstract Task<TSettings> LoadSettingsAsync(IServiceScope scope, CancellationToken ct);
    protected abstract Task SaveSettingsAsync(IServiceScope scope, TSettings settings, CancellationToken ct);
    protected abstract Task<object> RunOnceAsync(IServiceScope scope, SyncTriggerKind trigger, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool caughtUp = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            TSettings settings;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                settings = await LoadSettingsAsync(scope, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{JobKey}] načtení settings selhalo", JobKey);
                await SafeDelay(DisabledCheckInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (!settings.IsEnabled)
            {
                await SafeDelay(DisabledCheckInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (settings.PeriodMinutes <= 0)
            {
                _logger.LogWarning("[{JobKey}] PeriodMinutes <= 0, čekám 60s a zkusím znovu", JobKey);
                await SafeDelay(DisabledCheckInterval, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var period = TimeSpan.FromMinutes(settings.PeriodMinutes);
            var now = _timeProvider.GetUtcNow();

            // Catch-up on startup (běží jen jednou, pak loop pokračuje normal)
            if (!caughtUp)
            {
                caughtUp = true;
                if (settings.LastRunAt is null ||
                    now.UtcDateTime - settings.LastRunAt.Value > period)
                {
                    await ExecuteTickAsync(SyncTriggerKind.Auto, stoppingToken).ConfigureAwait(false);
                    continue;  // reload settings po běhu
                }
            }

            var next = SyncScheduleCalculator.ComputeNext(now, settings.AnchorAt, period);
            var delay = next - now;
            // If we're exactly on a tick boundary or in the past, we've just fired that tick
            // (or we're supposed to fire it now). Execute once and jump to the NEXT boundary
            // to avoid tight-looping on the same scheduled time when monotonic clock is frozen.
            if (delay <= TimeSpan.Zero)
            {
                await ExecuteTickAsync(SyncTriggerKind.Auto, stoppingToken).ConfigureAwait(false);
                next += period;
                delay = next - _timeProvider.GetUtcNow();
                if (delay <= TimeSpan.Zero) continue;
            }

            // Wait for next tick OR manual signal — whichever comes first.
            var trigger = await WaitForNextTriggerAsync(delay, stoppingToken).ConfigureAwait(false);
            if (stoppingToken.IsCancellationRequested) break;
            if (trigger is null) continue;   // cancellation / fallthrough
            await ExecuteTickAsync(trigger.Value, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<SyncTriggerKind?> WaitForNextTriggerAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var delayTask = Task.Delay(delay, _timeProvider, cts.Token);
        var signalTask = _manualSignal.WaitAsync(cts.Token);

        Task winner;
        try
        {
            winner = await Task.WhenAny(delayTask, signalTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            cts.Cancel();
        }

        // Swallow the loser's cancellation noise.
        try { await delayTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        try { await signalTask.ConfigureAwait(false); } catch (OperationCanceledException) { }

        if (stoppingToken.IsCancellationRequested) return null;
        return winner == signalTask ? SyncTriggerKind.Manual : SyncTriggerKind.Auto;
    }

    /// <summary>
    /// Orchestrace jednoho běhu: semafor, IsRunning flag, RunOnceAsync, SaveSettings s LastResultJson.
    /// Pokud semafor je zabraný → skip (log info).
    /// </summary>
    public async Task<bool> ExecuteTickAsync(SyncTriggerKind trigger, CancellationToken ct)
    {
        if (!_runLock.TryAcquire())
        {
            _logger.LogInformation("[{JobKey}] tick {Trigger} skipped — už běží", JobKey, trigger);
            return false;
        }

        var startedAt = _timeProvider.GetUtcNow().UtcDateTime;
        using var scope = _scopeFactory.CreateScope();
        TSettings settings;
        try
        {
            settings = await LoadSettingsAsync(scope, ct).ConfigureAwait(false);

            // Stale IsRunning flag recovery: pokud app spadla uprostřed předchozího běhu
            if (settings.IsRunning && settings.RunStartedAt is not null &&
                startedAt - settings.RunStartedAt.Value > StaleRunGuard)
            {
                _logger.LogWarning("[{JobKey}] clearing stale IsRunning flag (>{Min}min)", JobKey, StaleRunGuard.TotalMinutes);
            }

            settings.IsRunning = true;
            settings.RunStartedAt = startedAt;
            settings.LastTriggerKind = trigger.ToWireString();
            settings.UpdatedAt = startedAt;
            await SaveSettingsAsync(scope, settings, ct).ConfigureAwait(false);

            object result;
            try
            {
                result = await RunOnceAsync(scope, trigger, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{JobKey}] RunOnceAsync vyhodil výjimku", JobKey);
                result = new { error = ex.GetType().Name, message = ex.Message };
            }

            var finishedAt = _timeProvider.GetUtcNow().UtcDateTime;
            settings.IsRunning = false;
            settings.RunStartedAt = null;
            settings.LastRunAt = finishedAt;
            settings.LastResultJson = JsonSerializer.Serialize(result);
            settings.UpdatedAt = finishedAt;
            await SaveSettingsAsync(scope, settings, ct).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task SafeDelay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* stopping */ }
    }
}
