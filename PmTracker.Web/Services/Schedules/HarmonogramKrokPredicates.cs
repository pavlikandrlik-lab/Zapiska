using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Predikáty nad kroky harmonogramu (datum-model). Záznam patří do harmonogramu jen tehdy,
/// má-li aspoň jednu vyplněnou hodnotu kroku — plán NEBO skutečnost (i jen harvestovanou
/// z napojených externích záznamů). Jediný zdroj pravdy pro zařazení do projektové záložky
/// harmonogram i pro viditelnost překlikového tlačítka na kartě záznamu.
/// </summary>
public static class HarmonogramKrokPredicates
{
    /// <summary>true, pokud má aspoň jeden krok vyplněné <c>PlanDatum</c> nebo <c>SkutecnostDatum</c>.</summary>
    public static bool MaVyplnenouHodnotu(IEnumerable<ZaznamHarmonogramKrokEntity> kroky)
        => kroky.Any(k => k.PlanDatum.HasValue || k.SkutecnostDatum.HasValue);
}
