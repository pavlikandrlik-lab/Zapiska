using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.Sync;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ActiveDirectory;

public sealed class AdSyncJobAdminHandler : ISyncJobAdminHandler
{
    private static readonly TimeSpan ManualFloor = TimeSpan.FromMinutes(1);

    private readonly PmTrackerDbContext _db;
    private readonly ISyncJobRunLock<AdSyncSettingsEntity> _runLock;
    private readonly ManualTriggerSignal<AdSyncSettingsEntity> _manualSignal;
    private readonly TimeProvider _time;

    public AdSyncJobAdminHandler(
        PmTrackerDbContext db,
        ISyncJobRunLock<AdSyncSettingsEntity> runLock,
        ManualTriggerSignal<AdSyncSettingsEntity> manualSignal,
        TimeProvider time)
    {
        _db = db;
        _runLock = runLock;
        _manualSignal = manualSignal;
        _time = time;
    }

    public string JobKey => "ad.periodic";

    public async Task<SyncJobSettingsCardViewModel> LoadCardAsync(bool canManage, CancellationToken ct)
    {
        var s = await _db.AdSyncSettings.AsNoTracking().FirstAsync(x => x.Id == 1, ct);

        SyncJobResultSummary? summary = null;
        if (!string.IsNullOrWhiteSpace(s.LastResultJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(s.LastResultJson);
                var root = doc.RootElement;
                summary = new SyncJobResultSummary
                {
                    StartedAt = TryGetDateTime(root, "startedAt") ?? TryGetDateTime(root, "StartedAt"),
                    FinishedAt = TryGetDateTime(root, "finishedAt") ?? TryGetDateTime(root, "FinishedAt"),
                    DurationMs = TryGetInt64(root, "durationMs") ?? TryGetInt64(root, "DurationMs"),
                    OkCount = TryGetInt32(root, "okCount") ?? TryGetInt32(root, "OkCount"),
                    ErrorCount = TryGetInt32(root, "errorCount") ?? TryGetInt32(root, "ErrorCount"),
                    ErrorSummary = TryGetFirstErrorReason(root)
                };
            }
            catch (JsonException)
            {
                // malformed JSON — ignore, karta prostě neukáže summary.
            }
        }

        return new SyncJobSettingsCardViewModel
        {
            JobKey = JobKey,
            Title = "Synchronizace s AD",
            Description = "Pravidelná aktualizace osob s vyplněným GuidAd.",
            IsEnabled = s.IsEnabled,
            PeriodMinutes = s.PeriodMinutes,
            AnchorAt = s.AnchorAt,
            LastRunAt = s.LastRunAt,
            LastTriggerKind = s.LastTriggerKind,
            LastResult = summary,
            IsRunning = s.IsRunning,
            RunStartedAt = s.RunStartedAt,
            CanManage = canManage
        };
    }

    public async Task<bool> SaveAsync(SyncJobSettingsInputModel input, int? editorOsobaId, CancellationToken ct)
    {
        if (input.JobKey != JobKey)
        {
            return false;
        }

        var s = await _db.AdSyncSettings.FirstAsync(x => x.Id == 1, ct);
        s.IsEnabled = input.IsEnabled;
        s.PeriodMinutes = input.PeriodMinutes;
        s.AnchorAt = input.AnchorAt;
        s.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        s.UpdatedByOsobaId = editorOsobaId;
        await _db.SaveChangesAsync(ct);

        // Pokud admin právě zapnul job nebo změnil periodu, probudíme hosted service
        // aby načetla nové settings bez čekání na předchozí delay.
        _manualSignal.Signal();

        return true;
    }

    public async Task<ManualRunOutcome> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct)
    {
        // 1-min hard floor (amendment §13.3)
        var settings = await _db.AdSyncSettings.AsNoTracking().FirstAsync(x => x.Id == 1, ct);
        if (settings.LastRunAt.HasValue)
        {
            var elapsed = _time.GetUtcNow().UtcDateTime - settings.LastRunAt.Value;
            if (elapsed < ManualFloor)
            {
                var retryAfter = (int)Math.Ceiling((ManualFloor - elapsed).TotalSeconds);
                return new ManualRunOutcome(
                    Accepted: false,
                    Message: $"Sync proběhl před {(int)elapsed.TotalSeconds} s. Zkus za {retryAfter} s.");
            }
        }

        // Pokud job momentálně běží, neblokujeme — vrátíme jasnou zprávu.
        if (!_runLock.TryAcquire())
        {
            return new ManualRunOutcome(
                Accepted: false,
                Message: "Sync právě běží. Počkej na dokončení.");
        }
        _runLock.Release();

        // Probudí SyncHostedServiceBase loop — ten okamžitě spustí RunOnceAsync
        // s triggerem Manual (§13.2).
        _manualSignal.Signal();

        return new ManualRunOutcome(Accepted: true, Message: "Manual sync naplánován.");
    }

    private static DateTime? TryGetDateTime(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.String && p.TryGetDateTime(out var dt)
            ? dt
            : null;

    private static long? TryGetInt64(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.Number && p.TryGetInt64(out var v)
            ? v
            : null;

    private static int? TryGetInt32(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.Number && p.TryGetInt32(out var v)
            ? v
            : null;

    private static string? TryGetFirstErrorReason(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errs) && !root.TryGetProperty("Errors", out errs))
        {
            return null;
        }

        if (errs.ValueKind != JsonValueKind.Array || errs.GetArrayLength() == 0)
        {
            return null;
        }

        var first = errs[0];
        if (first.TryGetProperty("reason", out var r) || first.TryGetProperty("Reason", out r))
        {
            return r.GetString();
        }

        return null;
    }
}
