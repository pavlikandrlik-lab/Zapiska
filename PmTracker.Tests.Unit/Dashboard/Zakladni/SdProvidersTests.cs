using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;
using PmTracker.Web.Services.ServiceDesk.Prodleni;
using static PmTracker.Tests.Unit.Dashboard.Zakladni.ZakladniTestData;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>Sekce „ServiceDesk a dodavatelé" (spec 2026-10-15 §11.2, vizuály 16–19).</summary>
public sealed class SdProvidersTests
{
    private static ZakladniDataset S(IReadOnlyList<SynchronizovanyTicket>? otevrene = null,
        IReadOnlyList<PenalizaceVObdobi>? penalizace = null, IReadOnlyList<AkceptovanaKalkulaceDto>? kalkulace = null)
        => Dataset() with
        {
            ServiceDesk = new ServiceDeskSlice(
                new ProdleniPrehled(ProdleniStavDat.Ok, 1, "FIS", [], [], [], otevrene ?? [], penalizace ?? []),
                null, kalkulace ?? []),
        };

    [Fact]
    public void OpenByType_stack_u_dodavatele_u_nas()
    {
        var ds = S(otevrene:
        [
            new(1, "NES", true), new(2, "NES", false), new(3, "NES", true),
            new(4, "PMP", false), new(5, "PNF", true), new(6, "XYZ", true),
        ]);

        var data = new SdOpenTicketsByTypeProvider().Build(ds);

        data.Key.Should().Be("sd-open-by-type");
        data.Kind.Should().Be(ChartKind.StackedBar);
        data.Categories.Should().Equal("NES", "PMP", "PNF");
        data.Series.Select(s => s.Label).Should().Equal("U dodavatele", "U nás");
        data.Series[0].Values.Should().Equal(2, 0, 1);
        data.Series[1].Values.Should().Equal(1, 1, 0);
    }

    [Fact]
    public void PenaltyBySupplier_jen_dny_v_obdobi_serazeno()
    {
        var ds = S(penalizace:
        [
            new(1, "ACME", [new DateTime(2026, 1, 5), new DateTime(2026, 1, 6), new DateTime(2025, 12, 30)]),
            new(2, "BETA", [new DateTime(2026, 2, 1), new DateTime(2026, 2, 2), new DateTime(2026, 2, 3)]),
            new(3, "ACME", [new DateTime(2026, 4, 1)]),
            new(4, null, [new DateTime(2026, 3, 1)]),
        ]);

        var data = new SdPenaltyBySupplierProvider().Build(ds);

        data.Key.Should().Be("sd-penalty-by-supplier");
        data.Kind.Should().Be(ChartKind.HorizontalBar);
        data.Unit.Should().Be("dní");
        data.Categories.Should().Equal("BETA", "ACME", "(neuvedeno)");
        data.Series.Single().Values.Should().Equal(3, 2, 1);
    }

    [Fact]
    public void PenaltyPerMonth_datovane_dny()
    {
        var ds = S(penalizace:
        [
            new(1, "ACME", [new DateTime(2026, 2, 28), new DateTime(2026, 3, 2)]),
            new(2, "BETA", [new DateTime(2026, 3, 3), new DateTime(2026, 3, 4), new DateTime(2025, 12, 1)]),
        ]);

        var data = new SdPenaltyPerMonthProvider().Build(ds);

        data.Key.Should().Be("sd-penalty-per-month");
        data.Categories.Should().Equal("01/2026", "02/2026", "03/2026");
        data.Series.Single().Values.Should().Equal(0, 1, 3);
        data.Unit.Should().Be("dní");
    }

    [Fact]
    public void AcceptedCalculations_soucet_po_mesicich_kc()
    {
        var ds = S(kalkulace:
        [
            new(1, new DateTime(2026, 1, 10), 10_000m),
            new(2, new DateTime(2026, 1, 20), 5_500m),
            new(3, new DateTime(2026, 3, 1), 20_000m),
        ]);

        var data = new SdAcceptedCalculationsProvider().Build(ds);

        data.Key.Should().Be("sd-accepted-calculations");
        data.Unit.Should().Be("Kč");
        data.Series.Single().Values.Should().Equal(15_500, 0, 20_000);
        data.Insight.Should().Contain("35");
    }

    [Fact]
    public void Sd_providery_bez_slice_nespadnou()
    {
        IZakladniChartProvider[] providery =
            [new SdOpenTicketsByTypeProvider(), new SdPenaltyBySupplierProvider(), new SdPenaltyPerMonthProvider(), new SdAcceptedCalculationsProvider()];

        foreach (var p in providery)
        {
            var data = p.Build(Dataset());
            data.IsEmpty.Should().BeTrue(p.Key);
            p.SectionKey.Should().Be("servicedesk");
        }
    }
}
