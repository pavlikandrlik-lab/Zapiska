namespace PmTracker.Web.Models.ViewModels.SDConnector;

/// <summary>
/// Diagnostická stránka /SDConnector — kontrola harvest stavu pro admina.
/// Per spec §7 plánu, Task 19.
/// </summary>
public sealed class SDConnectorViewModel
{
    public bool TicketingEnabled { get; set; }
    public string? TicketingConnectionName { get; set; }
    public int ExterniOdkazCount { get; set; }
    public int HarvestedInLastDayCount { get; set; }
    public int NeverHarvestedCount { get; set; }

    public IReadOnlyList<SDConnectorExterniOdkazRow> Rows { get; set; } = Array.Empty<SDConnectorExterniOdkazRow>();
    public string? LastError { get; set; }
}

public sealed class SDConnectorExterniOdkazRow
{
    public int ExterniOdkazId { get; set; }
    public int ZaznamId { get; set; }
    public int ProjektId { get; set; }
    public string Cislo { get; set; } = string.Empty;
    public DateTime? LastHarvestedAt { get; set; }
    public int ActiveBindingsCount { get; set; }
    public int AutoBindingsCount { get; set; }
    public int ManualBindingsCount { get; set; }
}
