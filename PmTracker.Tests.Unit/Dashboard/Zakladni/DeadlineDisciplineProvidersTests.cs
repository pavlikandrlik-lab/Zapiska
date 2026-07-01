using System.Globalization;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// Golden-vektory termínové disciplíny. Každá metrika = vlastní graf (vlastní osa Y):
/// dodržení původního / posledního termínu (%), ø posun (dní), ø počet posunů. Plus souhrnné
/// stat-karty. Původní termín = první změna (nebo aktuální termín, pokud se neposouvalo);
/// poslední = record.DatumUkonceni; „dodržel" = DatumDokonceni ≤ termín; null = nedokončeno
/// → mimo jmenovatel dodržení; ø posun/počet jen přes posunuté záznamy.
/// </summary>
public sealed class DeadlineDisciplineProvidersTests
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    // R1: sub A, termín 10.3., dokončeno 5.3. (včas), bez posunu.
    // R2: sub A, termín 20.4. (posunuto z 10.4.), dokončeno 25.4. (pozdě), 1 posun = 10 dní.
    // R3: sub B, termín 1.5., nedokončeno (null) → mimo dodržení.
    private static ZakladniDataset Dataset() => new()
    {
        Obdobi = Obdobi.Rok(2026),
        Subsystemy = new[] { new DatasetSubsystem(1, "A", "Subsystém A"), new DatasetSubsystem(2, "B", "Subsystém B") },
        Records = new[]
        {
            new DatasetRecord(1, 1, null, new DateTime(2026, 1, 1), new DateTime(2026, 3, 10), new DateTime(2026, 3, 5)),
            new DatasetRecord(2, 1, null, new DateTime(2026, 1, 1), new DateTime(2026, 4, 20), new DateTime(2026, 4, 25)),
            new DatasetRecord(3, 2, null, new DateTime(2026, 1, 1), new DateTime(2026, 5, 1), null)
        },
        TerminChanges = new[]
        {
            new DatasetTerminChange(2, new DateTime(2026, 2, 1), new DateTime(2026, 4, 10), new DateTime(2026, 4, 20))
        }
    };

    [Fact]
    public void Stats_summarize_discipline()
    {
        var provider = new DeadlineDisciplineStatsProvider();
        provider.SectionKey.Should().Be("terminy");
        var data = provider.Build(Dataset());

        data.Kind.Should().Be(ChartKind.StatCards);
        data.Key.Should().Be("deadline-discipline-stats");
        // dokončené = R1,R2 → dodržel poslední: jen R1 = 50 %; dodržel původní: jen R1 = 50 %.
        // posunuté = R2 → ø posun 10 dní, ø počet 1.
        data.Stats.Select(s => s.Value).Should().Equal(
            "50,0 %",   // dodržel původní
            "50,0 %",   // dodržel poslední
            10.0.ToString("0.0", Cs),  // ø posun (dní)
            1.0.ToString("0.0", Cs));  // ø počet posunů
    }

    [Fact]
    public void Compliance_posledni_per_subsystem_is_own_chart()
    {
        var provider = new DeadlineCompliancePerSubsystemProvider();
        provider.Order.Should().Be(20);
        var data = provider.Build(Dataset());
        data.Kind.Should().Be(ChartKind.Bar);
        data.Key.Should().Be("deadline-compliance-posledni");
        data.Categories.Should().Equal("Subsystém A", "Subsystém B");
        data.Series[0].Values.Should().Equal(50d, 0d); // A: 1/2 včas; B: 0 dokončených → 0
    }

    [Fact]
    public void Compliance_puvodni_per_subsystem_is_own_chart()
    {
        var data = new DeadlineOriginalCompliancePerSubsystemProvider().Build(Dataset());
        data.Key.Should().Be("deadline-compliance-puvodni");
        data.Series[0].Values.Should().Equal(50d, 0d);
    }

    [Fact]
    public void Avg_shift_days_per_subsystem_is_own_chart()
    {
        var data = new DeadlineAvgShiftDaysPerSubsystemProvider().Build(Dataset());
        data.Key.Should().Be("deadline-avg-shift-days");
        data.Series[0].Values.Should().Equal(10d, 0d); // A: R2 posun 10 dní; B: bez posunu → 0
    }
}
