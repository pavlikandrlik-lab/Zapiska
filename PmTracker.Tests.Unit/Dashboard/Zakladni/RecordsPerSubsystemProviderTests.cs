using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// Golden-vektor: počet záznamů per subsystém (sloupec). Kategorie = názvy subsystémů
/// (v pořadí katalogu), hodnoty = počty. Subsystém bez záznamů = 0.
/// </summary>
public sealed class RecordsPerSubsystemProviderTests
{
    private static DatasetRecord Rec(int id, int subsystemId)
        => new(id, subsystemId, null, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

    [Fact]
    public void Counts_records_per_subsystem()
    {
        var ds = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2026),
            Subsystemy = new[] { new DatasetSubsystem(1, "A", "Subsystém A"), new DatasetSubsystem(2, "B", "Subsystém B") },
            Records = new[] { Rec(1, 1), Rec(2, 1), Rec(3, 2) }
        };

        var data = new RecordsPerSubsystemProvider().Build(ds);

        data.Kind.Should().Be(ChartKind.Bar);
        data.Key.Should().Be("records-per-subsystem");
        data.Categories.Should().Equal("Subsystém A", "Subsystém B");
        data.Series[0].Values.Should().Equal(2d, 1d);
    }

    [Fact]
    public void Subsystem_without_records_is_zero()
    {
        var ds = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2026),
            Subsystemy = new[] { new DatasetSubsystem(1, "A", "Subsystém A"), new DatasetSubsystem(2, "B", "Subsystém B") },
            Records = new[] { Rec(1, 1) }
        };

        var data = new RecordsPerSubsystemProvider().Build(ds);

        data.Series[0].Values.Should().Equal(1d, 0d);
    }
}
