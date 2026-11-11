namespace PmTracker.ServiceDesk.Contracts;

/// <summary>Akceptovaná kalkulace ticketu IS (Statistiky — akceptované kalkulace po měsících).</summary>
public sealed record AkceptovanaKalkulaceDto(int TicketId, DateTime DatumAkceptace, decimal Cena);
