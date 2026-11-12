namespace PmTracker.ServiceDesk.Contracts;

/// <summary>Předávka z <c>ticket_external_workflow</c> (U1–U11).</summary>
public sealed record WorkflowUdalostDto(int Id, DateTime Datum, int? TransferType, string? Supplier, DateTime? DtParam);
