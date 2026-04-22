using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels.Sync;

public sealed class SyncJobSettingsInputModel
{
    [Required]
    public string JobKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    [Range(5, 10080)]   // 5 minut až 7 dnů
    public int PeriodMinutes { get; set; }

    [Required]
    public DateTimeOffset AnchorAt { get; set; }
}
