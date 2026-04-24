namespace PmTracker.Web.Models.Entities;

/// <summary>
/// Plán 4 Feature C Task 1 — zdroj datumu skutečnosti pro krok harmonogramu.
/// Ukládáno jako byte (TINYINT) ve sloupci <c>skutecnost_zdroj</c>
/// na tabulce <c>zaznam_harmonogram_hodnoty</c>.
/// </summary>
public enum SkutecnostZdrojEnum : byte
{
    /// <summary>Prázdná skutečnost — žádná hodnota / žádný zdroj.</summary>
    Neznamo = 0,

    /// <summary>Auto-fillovaný ze ServiceDesk bindingu (vyjádření matching predikátu matice).</summary>
    Automat = 1,

    /// <summary>Ručně vyplněný přes switch = Ručně + explicitní input od uživatele.</summary>
    Manual = 2,

    /// <summary>
    /// Migrovaná data před zavedením auto-fill (2026-04-24).
    /// Pattern: HS0X_DELAY s nenulovou hodnotou, bez vazby na vyjádření.
    /// </summary>
    Historicka = 3,
}
