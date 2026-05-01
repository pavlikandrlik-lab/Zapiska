using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels.Sync;

namespace PmTracker.Web.Services.Sync;

/// <summary>
/// Review finding Q-4/A-1: sdílená implementace 3 admin handlerů
/// (AD, SD active, SD archive). Každý potomek specifikuje jen:
/// <list type="bullet">
///  <item><see cref="JobKey"/>, <see cref="Title"/>, <see cref="Description"/></item>
///  <item><see cref="Query"/> + <see cref="LoadForUpdateAsync"/> — DbSet přístup</item>
///  <item><see cref="OkCountFields"/> — volitelné mapování "OkCount" field names</item>
/// </list>
/// Shared: ManualFloor, TryAcquire/Release pro 429 check, settings load/save,
/// audit timestamp, ManualTriggerSignal.
/// </summary>
public abstract class SyncJobAdminHandlerBase<TSettings> : ISyncJobAdminHandler
    where TSettings : class, ISyncJobSettings
{
    protected static readonly TimeSpan ManualFloor = TimeSpan.FromMinutes(1);

    protected readonly PmTrackerDbContext Db;
    protected readonly ISyncJobRunLock<TSettings> RunLock;
    protected readonly ManualTriggerSignal<TSettings> ManualSignal;
    protected readonly TimeProvider Time;

    protected SyncJobAdminHandlerBase(
        PmTrackerDbContext db,
        ISyncJobRunLock<TSettings> runLock,
        ManualTriggerSignal<TSettings> manualSignal,
        TimeProvider time)
    {
        Db = db;
        RunLock = runLock;
        ManualSignal = manualSignal;
        Time = time;
    }

    public abstract string JobKey { get; }
    protected abstract string Title { get; }
    protected abstract string Description { get; }

    /// <summary>AsNoTracking read query (LoadCardAsync, TriggerManualRunAsync).</summary>
    protected abstract IQueryable<TSettings> Query();

    /// <summary>Tracked query pro SaveAsync update.</summary>
    protected abstract Task<TSettings> LoadForUpdateAsync(CancellationToken ct);

    /// <summary>Field names pro OkCount fallback chain (defaultně jen "okCount").</summary>
    protected virtual IReadOnlyList<string> OkCountFields { get; } = Array.Empty<string>();

    public async Task<SyncJobSettingsCardViewModel> LoadCardAsync(bool canManage, CancellationToken ct)
    {
        var s = await Query().AsNoTracking().FirstAsync(ct).ConfigureAwait(false);
        var summary = SyncJobResultJson.ParseSummary(s.LastResultJson, OkCountFields);

        return new SyncJobSettingsCardViewModel
        {
            JobKey = JobKey,
            Title = Title,
            Description = Description,
            IsEnabled = s.IsEnabled,
            PeriodMinutes = s.PeriodMinutes,
            AnchorAt = s.AnchorAt,
            LastRunAt = s.LastRunAt,
            LastTriggerKind = s.LastTriggerKind,
            LastResult = summary,
            IsRunning = s.IsRunning,
            RunStartedAt = s.RunStartedAt,
            CanManage = canManage,
            UpcomingRuns = ComputeUpcomingRuns(s, count: 4)
        };
    }

    /// <summary>
    /// Vypočítá nejbližší <paramref name="count"/> naplánovaných spuštění z AnchorAt + Period.
    /// Vrací prázdný seznam pokud je job vypnutý nebo perioda nevalidní —
    /// disabled job nemá příští plánovaný čas, dokud admin neaktivuje.
    /// </summary>
    private IReadOnlyList<DateTimeOffset> ComputeUpcomingRuns(TSettings s, int count)
    {
        if (!s.IsEnabled || s.PeriodMinutes <= 0)
        {
            return Array.Empty<DateTimeOffset>();
        }

        var period = TimeSpan.FromMinutes(s.PeriodMinutes);
        var now = Time.GetUtcNow();
        var result = new List<DateTimeOffset>(count);
        var cursor = now;
        for (int i = 0; i < count; i++)
        {
            var next = SyncScheduleCalculator.ComputeNext(cursor, s.AnchorAt, period);
            result.Add(next);
            // Posuneme cursor o tick za "next", aby další ComputeNext vrátil další slot.
            cursor = next.AddTicks(1);
        }
        return result;
    }

    public async Task<bool> SaveAsync(SyncJobSettingsInputModel input, int? editorOsobaId, CancellationToken ct)
    {
        if (input.JobKey != JobKey)
        {
            return false;
        }

        var s = await LoadForUpdateAsync(ct).ConfigureAwait(false);
        s.IsEnabled = input.IsEnabled;
        s.PeriodMinutes = input.PeriodMinutes;
        s.AnchorAt = input.AnchorAt;
        s.UpdatedAt = Time.GetUtcNow().UtcDateTime;
        s.UpdatedByOsobaId = editorOsobaId;
        await Db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Pokud admin právě zapnul job nebo změnil periodu, probudíme hosted service
        // aby načetla nové settings bez čekání na předchozí delay.
        ManualSignal.Signal();

        return true;
    }

    public async Task<ManualRunOutcome> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct)
    {
        _ = editorOsobaId;

        // 1-min hard floor (amendment §13.3)
        var settings = await Query().AsNoTracking().FirstAsync(ct).ConfigureAwait(false);
        if (settings.LastRunAt.HasValue)
        {
            var elapsed = Time.GetUtcNow().UtcDateTime - settings.LastRunAt.Value;
            if (elapsed < ManualFloor)
            {
                var retryAfter = (int)Math.Ceiling((ManualFloor - elapsed).TotalSeconds);
                return new ManualRunOutcome(
                    Accepted: false,
                    Message: $"Sync proběhl před {(int)elapsed.TotalSeconds} s. Zkus za {retryAfter} s.");
            }
        }

        // Pokud job momentálně běží, neblokujeme — vrátíme jasnou zprávu.
        if (!RunLock.TryAcquire())
        {
            return new ManualRunOutcome(
                Accepted: false,
                Message: "Sync právě běží. Počkej na dokončení.");
        }
        RunLock.Release();

        // Probudí SyncHostedServiceBase loop — ten okamžitě spustí RunOnceAsync
        // s triggerem Manual (§13.2).
        ManualSignal.Signal();

        return new ManualRunOutcome(Accepted: true, Message: "Manual sync naplánován.");
    }
}
