using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// Golden-vektor: skládaný sloupec stavového rozpadu per subsystém. Kategorie = subsystémy
/// (katalog pořadí), 4 série (Nezahájeno/Rozpracováno/Hotovo/Zrušeno), hodnoty per subsystém.
/// </summary>
public sealed class RecordStatusBreakdownProviderTests
{
    private static DatasetRecord Rec(int id, int subsystemId, int? stavId)
        => new(id, subsystemId, stavId, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

    [Fact]
    public void Stacks_status_buckets_per_subsystem()
    {
        var ds = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2026),
            Subsystemy = new[] { new DatasetSubsystem(1, "A", "Subsystém A"), new DatasetSubsystem(2, "B", "Subsystém B") },
            Stavy = new[]
            {
                new DatasetState(10, "NEW", "Nový", IsFinal: false),
                new DatasetState(30, "DONE", "Hotovo", IsFinal: true)
            },
            Records = new[]
            {
                Rec(1, 1, 10),  // A / Nezahájeno
                Rec(2, 1, 30),  // A / Hotovo
                Rec(3, 1, 30),  // A / Hotovo
                Rec(4, 2, 10)   // B / Nezahájeno
            }
        };

        var data = new RecordStatusBreakdownProvider().Build(ds);

        data.Kind.Should().Be(ChartKind.StackedBar);
        data.Key.Should().Be("record-status-breakdown");
        data.Categories.Should().Equal("Subsystém A", "Subsystém B");

        data.Series.Select(s => s.Label).Should().Equal("Nezahájeno", "Rozpracováno", "Hotovo", "Zrušeno");
        data.Series[0].Values.Should().Equal(1d, 1d); // Nezahájeno: A=1, B=1
        data.Series[1].Values.Should().Equal(0d, 0d); // Rozpracováno
        data.Series[2].Values.Should().Equal(2d, 0d); // Hotovo: A=2
        data.Series[3].Values.Should().Equal(0d, 0d); // Zrušeno
    }
}
