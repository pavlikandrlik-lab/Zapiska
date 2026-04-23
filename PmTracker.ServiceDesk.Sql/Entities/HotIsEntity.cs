namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_IS — informační systémy (FIS, ISSP, X_FIS).
/// Aktivita je char(10), má trailing spaces — použít LIKE 'Aktivní%' nebo LTRIM/RTRIM.
/// </summary>
internal sealed class HotIsEntity
{
    public int Id { get; set; }
    public string? Nazev { get; set; }
    public string? Zkratka { get; set; }
    public string? Aktivita { get; set; }
    public decimal? Limit { get; set; }
    public decimal? Cerpani { get; set; }
    public string? Semafor { get; set; }
}
