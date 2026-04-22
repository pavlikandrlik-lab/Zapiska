using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Models.Entities;

public sealed class AdSyncSettingsEntity : ISyncJobSettings
{
    public int Id { get; set; }
    public bool IsEnabled { get; set; }
    public int PeriodMinutes { get; set; } = 360;
    public DateTimeOffset AnchorAt { get; set; }

    public DateTime? LastRunAt { get; set; }
    public string? LastTriggerKind { get; set; }
    public string? LastResultJson { get; set; }

    public bool IsRunning { get; set; }
    public DateTime? RunStartedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByOsobaId { get; set; }
}
