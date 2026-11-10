using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Dashboard;

namespace PmTracker.Tests.Unit.Dashboard;

/// <summary>
/// Nejbližší plánovaný milník pro pořadí v Přehledu se bere jen z kroků s vlastním plánem.
/// Dopočtený konec plánu kroku bez plánu (konec předchozího plánu nebo datum založení) milník není.
/// </summary>
public sealed class PriorityMilestoneTests
{
    private static readonly DateOnly Dnes = new(2026, 10, 26);

    private static ZaznamHarmonogramKrokEntity Krok(byte poradi, DateTime? plan, DateTime? skutecnost = null)
        => new() { ZaznamId = 1, Poradi = poradi, PlanDatum = plan, SkutecnostDatum = skutecnost };

    [Fact]
    public void Zaznam_zalozeny_dnes_bez_planu_nema_milnik()
    {
        var milnik = PriorityMatrixRebuildService.ResolveNearestFutureMilestoneDate(
            Dnes, new DateTime(2026, 10, 26), [Krok(1, null, new DateTime(2026, 10, 26))]);

        milnik.Should().BeNull();
    }

    [Fact]
    public void Zaznam_bez_kroku_nema_milnik()
    {
        PriorityMatrixRebuildService.ResolveNearestFutureMilestoneDate(Dnes, new DateTime(2026, 10, 26), [])
            .Should().BeNull();
    }

    [Fact]
    public void Milnik_je_nejblizsi_budouci_plan()
    {
        var milnik = PriorityMatrixRebuildService.ResolveNearestFutureMilestoneDate(
            Dnes,
            new DateTime(2026, 7, 1),
            [
                Krok(1, new DateTime(2026, 8, 1), new DateTime(2026, 8, 1)),
                Krok(2, null),
                Krok(3, new DateTime(2026, 11, 15)),
                Krok(4, new DateTime(2026, 12, 1))
            ]);

        milnik.Should().Be(new DateOnly(2026, 11, 15));
    }

    [Fact]
    public void Plan_dnes_je_milnik()
    {
        var milnik = PriorityMatrixRebuildService.ResolveNearestFutureMilestoneDate(
            Dnes, new DateTime(2026, 7, 1), [Krok(1, new DateTime(2026, 10, 26))]);

        milnik.Should().Be(Dnes);
    }
}
