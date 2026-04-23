namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_SUBSYSTEM — subsystémy informačních systémů.
/// Reálný PK je <c>zkratka</c> (nvarchar(5) NOT NULL).
/// </summary>
internal sealed class HotSubsystemEntity
{
    public int Id { get; set; }
    public string? Nazev { get; set; }
    public string Zkratka { get; set; } = string.Empty;
    public string? Aktivita { get; set; }
    public string? Dodavatel { get; set; }
    public bool? PriznakGdprSub { get; set; }
}
