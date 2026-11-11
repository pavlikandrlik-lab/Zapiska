namespace PmTracker.ServiceDesk.Contracts;

/// <summary>Ticket IS synchronizovaný s dodavatelem (má řádek v <c>ticket_external</c>).</summary>
/// <param name="Id">HOT_ZAZNAMY.id (6místné číslo ticketu).</param>
/// <param name="TypZaznamu">Normalizovaný typ (Trim + ToUpperInvariant): NES / PMP / PNF.</param>
/// <param name="TicketExternalId"><c>ticket_external.id</c> — klíč předávek.</param>
/// <param name="SupplierTag"><c>ticket_external.supplier_tag</c> — značka u dodavatele.</param>
public sealed record ProdleniTicketDto(int Id, string Pid, string TypZaznamu, string? Strucne, string? Dulezitost,
    string? Modul, string? Dodavatel, string? Stav, DateTime? Datum, int TicketExternalId, string? SupplierTag);
