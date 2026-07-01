using System.Globalization;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// Golden-vektory sekce „vyjádření": stat-karty (celkem + ø na záznam, ošetřené dělení nulou)
/// a sloupec počtu vyjádření per subsystém (vyjádření → záznam → subsystém).
/// </summary>
public sealed class VyjadreniProvidersTests
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    private static DatasetRecord Rec(int id, int subsystemId)
        => new(id, subsystemId, null, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

    private static DatasetVyjadreni Vyj(int id, int zaznamId)
        => new(id, zaznamId, new DateTime(2026, 1, 15));

    [Fact]
    public void Stats_report_total_and_average_per_record()
    {
        var ds = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2026),
            Records = new[] { Rec(1, 1), Rec(2, 1) },
            Vyjadreni = new[] { Vyj(1, 1), Vyj(2, 1), Vyj(3, 2) }
        };

        var data = new VyjadreniStatsProvider().Build(ds);

        data.Kind.Should().Be(ChartKind.StatCards);
        data.Key.Should().Be("vyjadreni-stats");
        data.Stats.Should().HaveCount(2);
        data.Stats[0].Value.Should().Be("3");
        data.Stats[1].Value.Should().Be(1.5.ToString("0.0", Cs)); // 3 / 2 záznamy
    }

    [Fact]
    public void Stats_average_is_zero_when_no_records()
    {
        var ds = new ZakladniDataset { Obdobi = Obdobi.Rok(2026) };

        var data = new VyjadreniStatsProvider().Build(ds);

        data.Stats[0].Value.Should().Be("0");
        data.Stats[1].Value.Should().Be(0.0.ToString("0.0", Cs));
    }

    [Fact]
    public void PerSubsystem_counts_vyjadreni_via_record()
    {
        var ds = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2026),
            Subsystemy = new[] { new DatasetSubsystem(1, "A", "Subsystém A"), new DatasetSubsystem(2, "B", "Subsystém B") },
            Records = new[] { Rec(1, 1), Rec(2, 2) },
            Vyjadreni = new[] { Vyj(1, 1), Vyj(2, 1), Vyj(3, 2) }
        };

        var data = new VyjadreniPerSubsystemProvider().Build(ds);

        data.Kind.Should().Be(ChartKind.Bar);
        data.Key.Should().Be("vyjadreni-per-subsystem");
        data.Categories.Should().Equal("Subsystém A", "Subsystém B");
        data.Series[0].Values.Should().Equal(2d, 1d);
    }
}
