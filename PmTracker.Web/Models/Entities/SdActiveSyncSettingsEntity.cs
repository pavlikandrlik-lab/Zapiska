using PmTracker.Web.Services.Sync;

namespace PmTracker.Web.Models.Entities;

/// <summary>
/// Singleton (id=1) settings pro periodic harvest aktivních SD tiketů
/// (HOT_ZAZNAMY.stav &lt;&gt; 'archiv'). Default perioda 60 min.
/// Implementuje <see cref="ISyncJobSettings"/> pro sdílenou sync infrastrukturu.
/// </summary>
public sealed class SdActiveSyncSettingsEntity : ISyncJobSettings
{
    public int Id { get; set; }
    public bool IsEnabled { get; set; }
    public int PeriodMinutes { get; set; } = 60;
    public DateTimeOffset AnchorAt { get; set; }

    public DateTime? LastRunAt { get; set; }
    public string? LastTriggerKind { get; set; }
    public string? LastResultJson { get; set; }

    public bool IsRunning { get; set; }
    public DateTime? RunStartedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByOsobaId { get; set; }
}
