namespace PmTracker.Web.Services.Records;

/// <summary>
/// Datum-model: odkud pochází skutečnost kroku harmonogramu. UI rozlišuje ikonu/tooltip.
/// </summary>
public enum ZdrojSkutecnosti : byte
{
    None = 0,
    /// <summary>Skutečnost plyne z Active vazby v <c>zaznam_harmonogram_vyjadreni_vazba</c>.</summary>
    FromVyjadreni = 1,
    /// <summary>Skutečnost byla zadána ručně (krok má skutečnost, ale vazba neexistuje).</summary>
    Manual = 2
}
