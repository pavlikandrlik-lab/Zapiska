using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;
using static PmTracker.Tests.Unit.Dashboard.Zakladni.ZakladniTestData;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>Sekce „Průběh práce" a „Termíny" (spec 2026-10-15 §11.2, vizuály 7–12).</summary>
public sealed class PrubehProvidersTests
{
    [Fact]
    public void BurnUp_kumulativni_po_mesicich()
    {
        var ds = Dataset(
        [
            Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2026, 1, 3)),
            Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2026, 1, 10)),
            Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2026, 1, 20)),
            Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2026, 2, 3)),
            Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2026, 2, 4)),
            Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2026, 3, 9)),
            Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2025, 12, 9)),
            Hotovy_(new DateTime(2026, 1, 15)),
            Hotovy_(new DateTime(2026, 2, 15)), Hotovy_(new DateTime(2026, 2, 16)),
            Hotovy_(new DateTime(2026, 3, 15)), Hotovy_(new DateTime(2026, 3, 16)),
            Hotovy_(new DateTime(2026, 3, 17), stav: Zruseny),
        ]);

        var data = new BurnUpProvider().Build(ds);

        data.Key.Should().Be("burn-up");
        data.Kind.Should().Be(ChartKind.Line);
        data.Categories.Should().Equal("01/2026", "02/2026", "03/2026");
        data.Series.Select(s => s.Label).Should().Equal("Založeno", "Dokončeno");
        data.Series[0].Values.Should().Equal(3, 5, 6);
        data.Series[1].Values.Should().Equal(1, 3, 5);
    }

    [Fact]
    public void BurnUp_rok_konci_dneskem()
    {
        var ds = Dataset([Otevreny(new DateTime(2026, 6, 1), zalozeno: new DateTime(2026, 1, 3))], Obdobi.Rok(2026));

        new BurnUpProvider().Build(ds).Categories.Should().HaveCount(3, "budoucí měsíce by kreslily vodorovnou čáru");
    }

    [Fact]
    public void Aging_kose()
    {
        var ds = Dataset(
        [
            Otevreny(Dnes.AddDays(5), zalozeno: Dnes.AddDays(-10)),
            Otevreny(Dnes.AddDays(5), zalozeno: Dnes.AddDays(-45)),
            Otevreny(Dnes.AddDays(5), zalozeno: Dnes.AddDays(-120)),
            Otevreny(Dnes.AddDays(5), zalozeno: Dnes.AddDays(-200)),
            Otevreny(Dnes.AddDays(5), zalozeno: Dnes.AddDays(-400)),
            Hotovy_(new DateTime(2026, 2, 1), zalozeno: Dnes.AddDays(-500)),
        ]);

        var data = new OpenTaskAgingProvider().Build(ds);

        data.Key.Should().Be("open-task-aging");
        data.Kind.Should().Be(ChartKind.Bar);
        data.Categories.Should().Equal("0–30 dní", "31–90 dní", "91–180 dní", "181–365 dní", "víc než rok");
        data.Series.Single().Values.Should().Equal(1, 1, 1, 1, 1);
    }

    [Fact]
    public void Compliance_seskupeny_dve_serie()
    {
        // Subsystém A: 4 dokončené; původní termín splní 2, poslední 3.
        var records = new[]
        {
            Hotovy_(new DateTime(2026, 2, 1), termin: new DateTime(2026, 2, 5)),
            Hotovy_(new DateTime(2026, 2, 1), termin: new DateTime(2026, 2, 5)),
            Hotovy_(new DateTime(2026, 2, 10), termin: new DateTime(2026, 2, 12)),
            Hotovy_(new DateTime(2026, 2, 20), termin: new DateTime(2026, 2, 12)),
        };
        var ds = Dataset(records) with
        {
            TerminChanges =
            [
                new DatasetTerminChange(records[2].Id, new DateTime(2026, 1, 20), new DateTime(2026, 2, 5), new DateTime(2026, 2, 12)),
                new DatasetTerminChange(records[3].Id, new DateTime(2026, 1, 20), new DateTime(2026, 2, 5), new DateTime(2026, 2, 12)),
            ],
        };

        var data = new DeadlineCompliancePerSubsystemProvider().Build(ds);

        data.Key.Should().Be("deadline-compliance-per-subsystem");
        data.Kind.Should().Be(ChartKind.Bar);
        data.Unit.Should().Be("%");
        data.Series.Select(s => s.Label).Should().Equal("Původní termín", "Poslední termín");
        data.Series[0].Values[0].Should().Be(50);
        data.Series[1].Values[0].Should().Be(75);
    }

    [Fact]
    public void Stav_ukolu_je_donut_se_stavovymi_barvami()
    {
        var data = new RecordStatusPieProvider().Build(Dataset([Otevreny(Dnes)]));

        data.Kind.Should().Be(ChartKind.Donut);
        data.SectionKeyOf(new RecordStatusPieProvider()).Should().Be("prubeh");
        data.CategoryColorTokens.Should().HaveCount(4).And.Contain("--background-status-success");
    }
}

internal static class ChartDataTestExtensions
{
    public static string SectionKeyOf(this ChartData _, IZakladniChartProvider provider) => provider.SectionKey;
}
