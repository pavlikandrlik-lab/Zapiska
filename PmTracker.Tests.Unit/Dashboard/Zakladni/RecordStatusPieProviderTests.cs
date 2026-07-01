using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// Golden-vektor: koláč stavového rozpadu za celý projekt. 4 segmenty v pevném pořadí
/// (Nezahájeno, Rozpracováno, Hotovo, Zrušeno), hodnoty = počty záznamů v kýblu.
/// </summary>
public sealed class RecordStatusPieProviderTests
{
    private static DatasetRecord Rec(int id, int? stavId)
        => new(id, 1, stavId, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1));

    [Fact]
    public void Counts_records_into_four_buckets()
    {
        var ds = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2026),
            Stavy = new[]
            {
                new DatasetState(10, "NEW", "Nový", IsFinal: false),
                new DatasetState(20, "WIP", "Rozpracováno", IsFinal: false),
                new DatasetState(30, "DONE", "Hotovo", IsFinal: true),
                new DatasetState(40, "CANCEL", "Zrušeno", IsFinal: true)
            },
            Records = new[]
            {
                Rec(1, 10),          // Nezahájeno
                Rec(2, 20), Rec(3, 20), // Rozpracováno ×2
                Rec(4, 30),          // Hotovo
                Rec(5, 40),          // Zrušeno
                Rec(6, null)         // neznámý stav → Nezahájeno
            }
        };

        var data = new RecordStatusPieProvider().Build(ds);

        data.Kind.Should().Be(ChartKind.Pie);
        data.Key.Should().Be("record-status-pie");
        data.Categories.Should().Equal("Nezahájeno", "Rozpracováno", "Hotovo", "Zrušeno");
        data.Series[0].Values.Should().Equal(2d, 2d, 1d, 1d);
    }
}
