using System.ComponentModel.DataAnnotations;

namespace PmTracker.ServiceDesk.Contracts;

public sealed class TicketingOptions
{
    public const string SectionName = "Ticketing";

    public bool Enabled { get; init; }

    [Required]
    public string ConnectionStringName { get; init; } = "TicketingReadOnly";

    public int CommandTimeoutSeconds { get; init; } = 30;
}
