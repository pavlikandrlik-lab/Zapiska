namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// Databáze ServiceDesku, dbo.ticket_external_workflow — předávky U1–U11 (spec 2026-10-15 §6.1).
/// Cizí klíč <c>id_ticket_external</c> → <c>ticket_external.id</c> (FK_ticket_external_workflow_ticket_external).
/// <c>dt_param</c> = termín plnění (U1/U2 u PMP/PNF) nebo schválený nový termín (U11).
/// </summary>
internal sealed class TicketExternalWorkflowEntity
{
    public int Id { get; set; }
    public int TicketExternalId { get; set; }
    public DateTime Date { get; set; }
    public int? TransferType { get; set; }
    public string? Supplier { get; set; }
    public DateTime? DtParam { get; set; }
}
