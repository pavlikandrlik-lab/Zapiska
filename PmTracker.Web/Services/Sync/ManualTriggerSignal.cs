namespace PmTracker.Web.Services.Sync;

/// <summary>
/// Per-job signal pro realtime probuzení periodické smyčky z admin UI („Spustit teď").
/// Používá <see cref="TaskCompletionSource"/> s RunContinuationsAsynchronously — každý
/// <see cref="Signal"/> uvolní max. 1 await čekající na <see cref="WaitAsync"/> a instance
/// se interně obnoví pro další čekání.
/// </summary>
public sealed class ManualTriggerSignal<TSettings>
    where TSettings : class, ISyncJobSettings
{
    private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken ct)
    {
        var tcs = Volatile.Read(ref _tcs);
        return tcs.Task.WaitAsync(ct);
    }

    public void Signal()
    {
        var current = Volatile.Read(ref _tcs);
        if (current.TrySetResult())
        {
            // Swap in a fresh TCS so next Wait-ers block on a new signal.
            var fresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.CompareExchange(ref _tcs, fresh, current);
        }
    }
}
