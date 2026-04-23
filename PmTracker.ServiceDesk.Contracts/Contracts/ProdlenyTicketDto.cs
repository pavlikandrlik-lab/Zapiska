namespace PmTracker.ServiceDesk.Contracts;

/// <summary>
/// Ticket v prodlení pro panel "NES v prodlení" v projektovém dashboardu.
/// Společný tvar pro NES/PMP/PNF — zdroj termínu (SLA nebo plánovaný)
/// je typově rozlišen přes <see cref="TypZaznamu"/>.
/// </summary>
public sealed record ProdlenyTicketDto(
    int Id,
    string Pid,
    string TypZaznamu,
    string? Strucne,
    string? Dulezitost,
    string? Zavaznost,
    string? Modul,
    string? Dodavatel,
    string? Stav,
    DateTime Termin,
    int DniProdleni);
