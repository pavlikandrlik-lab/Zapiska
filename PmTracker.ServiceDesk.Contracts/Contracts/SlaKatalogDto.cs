namespace PmTracker.ServiceDesk.Contracts;

/// <summary>Řádek <c>ticket_external_sla</c>; <paramref name="Id"/> rozhoduje kolize po ořezu mezer (nižší vyhrává, spec R8).</summary>
public sealed record SlaKatalogDto(int Id, string SlaName, double SlaHours);
