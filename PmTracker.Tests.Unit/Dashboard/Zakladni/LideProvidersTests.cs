using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;
using static PmTracker.Tests.Unit.Dashboard.Zakladni.ZakladniTestData;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>Sekce „Lidé a spolupráce" (spec 2026-10-15 §11.2, vizuály 13–14).</summary>
public sealed class LideProvidersTests
{
    [Fact]
    public void OwnerWorkload_top10_a_ostatni()
    {
        // Vlastník n má n otevřených úkolů; vlastník 12 má navíc 1 po termínu.
        var records = new List<DatasetRecord>();
        for (var vlastnik = 1; vlastnik <= 12; vlastnik++)
        {
            for (var i = 0; i < vlastnik; i++)
            {
                records.Add(Otevreny(Dnes.AddDays(10), vlastnik: vlastnik));
            }
        }
        records.Add(Otevreny(Dnes.AddDays(-3), vlastnik: 12));
        records.Add(Hotovy_(new DateTime(2026, 2, 1)));
        var ds = Dataset(records) with
        {
            Vlastnici = Enumerable.Range(1, 12).Select(i => new DatasetOsoba(i, $"Osoba {i:00}")).ToList(),
        };

        var data = new OwnerWorkloadProvider().Build(ds);

        data.Key.Should().Be("owner-workload");
        data.Kind.Should().Be(ChartKind.StackedHorizontalBar);
        data.Span.Should().Be(ChartSpan.Full);
        data.Categories.Should().HaveCount(11);
        data.Categories[0].Should().Be("Osoba 12");
        data.Categories[9].Should().Be("Osoba 03");
        data.Categories[10].Should().Be("Ostatní");
        data.Series.Select(s => s.Label).Should().Equal("V termínu", "Po termínu");
        data.Series[0].Values[0].Should().Be(12);
        data.Series[1].Values[0].Should().Be(1);
        data.Series[0].Values[10].Should().Be(3, "Ostatní = vlastníci 1 a 2");
    }

    [Fact]
    public void Attendance_procenta_po_mesicich()
    {
        var ds = Dataset() with
        {
            Jednani = [new DatasetJednani(1, new DateTime(2026, 1, 14)), new DatasetJednani(9, new DateTime(2025, 12, 10))],
            Ucasti =
            [
                .. Enumerable.Range(0, 8).Select(_ => new DatasetUcast(1, true)),
                .. Enumerable.Range(0, 2).Select(_ => new DatasetUcast(1, false)),
                new DatasetUcast(9, false),
            ],
        };

        var data = new AttendancePerMonthProvider().Build(ds);

        data.Key.Should().Be("attendance-per-month");
        data.Unit.Should().Be("%");
        data.Categories.Should().Equal("01/2026", "02/2026", "03/2026");
        data.Series.Single().Values.Should().Equal(80, 0, 0);
        data.Note.Should().Contain("0 %");
        data.Insight.Should().Contain("80");
    }

    [Fact]
    public void Attendance_nula_procent_je_udaj_ne_prazdny_graf()
    {
        var ds = Dataset() with
        {
            Jednani = [new DatasetJednani(1, new DateTime(2026, 2, 3))],
            Ucasti = [new DatasetUcast(1, false), new DatasetUcast(1, false)],
        };

        var data = new AttendancePerMonthProvider().Build(ds);

        data.Series.Single().Values.Should().Equal(0, 0, 0);
        data.IsEmpty.Should().BeFalse("jednání proběhlo, jen nikdo nebyl přítomen");
    }
}
