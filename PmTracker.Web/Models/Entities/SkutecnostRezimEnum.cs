namespace PmTracker.Web.Models.Entities;

/// <summary>
/// Plán 4 Feature C Task 1 — rezim plnění skutečnosti pro krok harmonogramu.
/// Ukládáno jako byte (TINYINT) ve sloupci <c>skutecnost_rezim</c>
/// na tabulce <c>zaznam_harmonogram_krok</c>.
/// </summary>
public enum SkutecnostRezimEnum : byte
{
    /// <summary>Default — auto-fill ze SD bindingů je aktivní; resolver přepisuje datum.</summary>
    Auto = 0,

    /// <summary>
    /// User přepnul na ruční zápis — auto-fill ignoruje tento řádek (sync skipne).
    /// Re-harvest nemůže zahodit Manual datum (respektuje user intent dle C-Q4).
    /// </summary>
    Manual = 1,
}
