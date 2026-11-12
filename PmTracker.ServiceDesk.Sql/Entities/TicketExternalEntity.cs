namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// Databáze ServiceDesku, dbo.ticket_external — ticket synchronizovaný s dodavatelem (spec 2026-10-15 §6.1).
/// <c>external_id</c> je <c>HOT_ZAZNAMY.id</c> jako text (varchar(50)); páruje se po Trim, nečíselné hodnoty se ignorují.
/// Mapují se jen sloupce, které výpočet prodlení používá.
/// </summary>
internal sealed class TicketExternalEntity
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = "";
    public int? ExternalSyncId { get; set; }
    public string? SupplierTag { get; set; }
    public string? SlaName { get; set; }
    public List<TicketExternalWorkflowEntity> Workflow { get; set; } = [];
}
