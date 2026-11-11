namespace PmTracker.ServiceDesk.Sql.Entities;

/// <summary>
/// Databáze ServiceDesku, dbo.ticket_external_sla — SLA hodiny podle důležitosti (spec 2026-10-15 §6.1).
/// <c>sla_name</c> (nvarchar(8)) nese hodnoty <c>HOT_ZAZNAMY.dulezitost</c> (NES I … Vada D).
/// </summary>
internal sealed class TicketExternalSlaEntity
{
    public int Id { get; set; }
    public string SlaName { get; set; } = "";
    public double SlaHours { get; set; }
}
