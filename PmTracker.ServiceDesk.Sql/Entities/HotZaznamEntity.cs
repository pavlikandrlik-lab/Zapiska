namespace PmTracker.ServiceDesk.Sql.Entities;

internal sealed class HotZaznamEntity
{
    public long Radek { get; set; }
    public string Id { get; set; } = string.Empty;
    public string? TypZaznamu { get; set; }
    public string? Strucne { get; set; }
    public string? Popis { get; set; }
    public int? Stav { get; set; }
    public int? Splneno { get; set; }
    public DateTime? SlaDeadline { get; set; }
}
