using System.Globalization;

namespace PmTracker.Web.Services.Data;

public sealed class DashboardPriorityOptions
{
    public const string SectionName = "PmTracker:DashboardPriority";

    public int PriorityHorizonDays { get; set; } = 30;
    public int PriorityOverdueCapDays { get; set; } = 30;
    public string PriorityNightlyRebuildTime { get; set; } = "02:00";
    public bool BootstrapFullRebuildOnStartup { get; set; } = true;

    public TimeOnly GetNightlyRebuildTime()
    {
        if (!TimeOnly.TryParseExact(
                PriorityNightlyRebuildTime,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var result))
        {
            throw new InvalidOperationException(
                $"Konfigurace {SectionName}:PriorityNightlyRebuildTime musí mít formát HH:mm. Aktuální hodnota: '{PriorityNightlyRebuildTime}'.");
        }

        return result;
    }
}

