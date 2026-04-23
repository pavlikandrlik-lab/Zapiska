namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// intranetNEW.dbo.HOT_MODULY — moduly pod subsystémy.
/// Join na HOT_ZAZNAMY je přes <c>zkratka</c> (99.7% match dle discovery).
/// FK na HOT_IS přes <c>id_IS</c>.
/// </summary>
internal sealed class HotModulyEntity
{
    public int Id { get; set; }
    public string? Modul { get; set; }
    public string Zkratka { get; set; } = string.Empty;
    public string? Subsystem { get; set; }
    public int? IdIS { get; set; }
    public string? Faze { get; set; }
    public string? Aktivita { get; set; }
    public string? Dodavatel { get; set; }
}
