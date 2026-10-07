using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy;

/// <summary>
/// Které místo plnění výzva tiskne (uživatel 2026-10-07). Výzva v Přípravě se řídí aktuálním
/// místem plnění projektu. Při opuštění Přípravy se hodnota uloží do snapshotu výzvy a dál se
/// nemění, takže odeslaný dokument jde vytisknout stejně i po změně projektu. Projekt bez místa
/// plnění (pole jde v editaci vyprázdnit) nechá uloženou hodnotu.
/// </summary>
public static class VyzvaMistoPlneni
{
    public static string Platne(VyzvaStav stav, string ulozene, string? projektu)
        => stav == VyzvaStav.Priprava && !string.IsNullOrWhiteSpace(projektu) ? projektu : ulozene;
}
