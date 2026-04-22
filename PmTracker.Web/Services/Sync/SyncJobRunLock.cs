namespace PmTracker.Web.Services.Sync;

public sealed class SyncJobRunLock<TSettings> : ISyncJobRunLock<TSettings>
    where TSettings : class, ISyncJobSettings
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public bool TryAcquire() => _semaphore.Wait(TimeSpan.Zero);

    public void Release()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Volání Release bez předchozího TryAcquire — silent no-op
        }
    }
}
