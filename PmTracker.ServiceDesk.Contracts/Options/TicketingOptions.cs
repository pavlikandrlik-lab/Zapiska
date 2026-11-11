using System.ComponentModel.DataAnnotations;

namespace PmTracker.ServiceDesk.Contracts;

public sealed class TicketingOptions
{
    public const string SectionName = "Ticketing";

    public bool Enabled { get; init; }

    [Required]
    public string ConnectionStringName { get; init; } = "TicketingReadOnly";

    /// <summary>
    /// Connection string k databázi starého ServiceDesku (předávky <c>ticket_external_workflow</c> a SLA hodiny
    /// <c>ticket_external_sla</c>), ze které se počítá prodlení ticketů (spec 2026-10-15 §6.2).
    /// Prázdná hodnota connection stringu = prodlení se nepočítá (Disabled fallback), zbytek integrace běží dál.
    /// </summary>
    [Required]
    public string ExternalConnectionStringName { get; init; } = "ServiceDeskExternalReadOnly";

    public int CommandTimeoutSeconds { get; init; } = 30;
}
