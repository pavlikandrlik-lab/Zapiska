using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.Sync;
using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Admin handler pro SD active periodic job. JobKey = "sd.active".
/// Paralelní s <see cref="PmTracker.Web.Services.ActiveDirectory.AdSyncJobAdminHandler"/>,
/// jen jiná entita a jiný label.
/// </summary>
public sealed class SdActiveSyncJobAdminHandler : ISyncJobAdminHandler
{
    private static readonly TimeSpan ManualFloor = TimeSpan.FromMinutes(1);

    private readonly PmTrackerDbContext _db;
    private readonly ISyncJobRunLock<SdActiveSyncSettingsEntity> _runLock;
    private readonly ManualTriggerSignal<SdActiveSyncSettingsEntity> _manualSignal;
    private readonly TimeProvider _time;

    public SdActiveSyncJobAdminHandler(
        PmTrackerDbContext db,
        ISyncJobRunLock<SdActiveSyncSettingsEntity> runLock,
        ManualTriggerSignal<SdActiveSyncSettingsEntity> manualSignal,
        TimeProvider time)
    {
        _db = db;
        _runLock = runLock;
        _manualSignal = manualSignal;
        _time = time;
    }

    public string JobKey => "sd.active";

    public async Task<SyncJobSettingsCardViewModel> LoadCardAsync(bool canManage, CancellationToken ct)
    {
        var s = await _db.SdActiveSyncSettings.AsNoTracking().FirstAsync(x => x.Id == 1, ct);

        SyncJobResultSummary? summary = ParseSummary(s.LastResultJson);

        return new SyncJobSettingsCardViewModel
        {
            JobKey = JobKey,
            Title = "ServiceDesk — aktivní tickety",
            Description = "Pravidelné načítání vyjádření z HOT_VYJADRENI pro aktivní tickety (stav <> 'archiv').",
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

        var s = await _db.SdActiveSyncSettings.FirstAsync(x => x.Id == 1, ct);
        s.IsEnabled = input.IsEnabled;
        s.PeriodMinutes = input.PeriodMinutes;
        s.AnchorAt = input.AnchorAt;
        s.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        s.UpdatedByOsobaId = editorOsobaId;
        await _db.SaveChangesAsync(ct);

        _manualSignal.Signal();
        return true;
    }

    public async Task<ManualRunOutcome> TriggerManualRunAsync(int? editorOsobaId, CancellationToken ct)
    {
        _ = editorOsobaId;

        var settings = await _db.SdActiveSyncSettings.AsNoTracking().FirstAsync(x => x.Id == 1, ct);
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

        if (!_runLock.TryAcquire())
        {
            return new ManualRunOutcome(
                Accepted: false,
                Message: "Sync právě běží. Počkej na dokončení.");
        }
        _runLock.Release();

        _manualSignal.Signal();
        return new ManualRunOutcome(Accepted: true, Message: "Manual sync naplánován.");
    }

    private static SyncJobResultSummary? ParseSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new SyncJobResultSummary
            {
                StartedAt = TryGetDateTime(root, "startedAt") ?? TryGetDateTime(root, "StartedAt"),
                FinishedAt = TryGetDateTime(root, "finishedAt") ?? TryGetDateTime(root, "FinishedAt"),
                DurationMs = TryGetInt64(root, "durationMs") ?? TryGetInt64(root, "DurationMs"),
                OkCount = TryGetInt32(root, "ticketsDrilled") ?? TryGetInt32(root, "TicketsDrilled") ?? TryGetInt32(root, "okCount") ?? TryGetInt32(root, "OkCount"),
                ErrorCount = TryGetInt32(root, "errorCount") ?? TryGetInt32(root, "ErrorCount"),
                ErrorSummary = TryGetFirstErrorReason(root)
            };
        }
        catch (JsonException)
        {
            return null;
        }
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
