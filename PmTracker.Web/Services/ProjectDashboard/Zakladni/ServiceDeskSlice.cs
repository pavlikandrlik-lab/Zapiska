using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Data ServiceDesku pro sekci „ServiceDesk a dodavatelé" Statistik (spec 2026-10-15 §11.3).
/// Při výpadku má <see cref="Prodleni"/> stav <see cref="ProdleniStavDat.Nedostupne"/> a sekce ukáže chybovou kartu.
/// </summary>
/// <param name="Prodleni">Přehled prodlení včetně ticketů archivovaných během období (penalizace v období).</param>
/// <param name="Rozpocet">Limit a čerpání IS; null = IS rozpočet nemá nebo ho nejde načíst.</param>
/// <param name="Kalkulace">Akceptované kalkulace ticketů IS s datem akceptace v období.</param>
public sealed record ServiceDeskSlice(ProdleniPrehled Prodleni, IsRozpocetDto? Rozpocet,
    IReadOnlyList<AkceptovanaKalkulaceDto> Kalkulace);
