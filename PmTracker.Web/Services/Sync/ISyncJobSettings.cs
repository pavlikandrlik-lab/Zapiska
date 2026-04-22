namespace PmTracker.Web.Services.Sync;

public interface ISyncJobSettings
{
    // Konfigurace
    bool IsEnabled { get; set; }
    int PeriodMinutes { get; set; }
    DateTimeOffset AnchorAt { get; set; }

    // Status posledního běhu
    DateTime? LastRunAt { get; set; }
    string? LastTriggerKind { get; set; }
    string? LastResultJson { get; set; }

    // Viditelnost běhu
    bool IsRunning { get; set; }
    DateTime? RunStartedAt { get; set; }

    // Audit
    DateTime UpdatedAt { get; set; }
    int? UpdatedByOsobaId { get; set; }
}
