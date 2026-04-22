namespace PmTracker.Web.Services.Sync;

public interface ISyncJobRunLock<TSettings>
    where TSettings : class, ISyncJobSettings
{
    /// <summary>Pokus o zamčení bez čekání. True = získáno, False = už běží.</summary>
    bool TryAcquire();

    /// <summary>Uvolnění. Musí se volat v finally po úspěšném TryAcquire.</summary>
    void Release();
}
