using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// 2026-06-29: záznam patří do harmonogramu (a má překlikové tlačítko), pokud má aspoň
/// jednu vyplněnou hodnotu kroku — plán NEBO skutečnost. Tj. i záznam bez plánu, který má
/// přes napojené externí záznamy vyplněnou skutečnost u kroků. Záznam bez čehokoli (ani
/// plán, ani skutečnost) do harmonogramu nepatří.
/// </summary>
public sealed class HarmonogramKrokPredicatesTests
{
    private static ZaznamHarmonogramKrokEntity Krok(DateTime? plan, DateTime? skutecnost) =>
        new() { Poradi = 1, PlanDatum = plan, SkutecnostDatum = skutecnost };

    [Fact]
    public void Prazdny_seznam_kroku_nema_hodnotu()
    {
        HarmonogramKrokPredicates.MaVyplnenouHodnotu(Array.Empty<ZaznamHarmonogramKrokEntity>())
            .Should().BeFalse();
    }

    [Fact]
    public void Vsechny_kroky_null_nemaji_hodnotu()
    {
        var kroky = new[] { Krok(null, null), Krok(null, null) };
        HarmonogramKrokPredicates.MaVyplnenouHodnotu(kroky).Should().BeFalse();
    }

    [Fact]
    public void Jen_plan_ma_hodnotu()
    {
        var kroky = new[] { Krok(new DateTime(2026, 1, 1), null), Krok(null, null) };
        HarmonogramKrokPredicates.MaVyplnenouHodnotu(kroky).Should().BeTrue();
    }

    [Fact]
    public void Jen_skutecnost_ma_hodnotu()
    {
        // Klíčový případ: záznam bez plánu, ale s harvestovanou skutečností z externích záznamů.
        var kroky = new[] { Krok(null, null), Krok(null, new DateTime(2026, 2, 1)) };
        HarmonogramKrokPredicates.MaVyplnenouHodnotu(kroky).Should().BeTrue();
    }
}
