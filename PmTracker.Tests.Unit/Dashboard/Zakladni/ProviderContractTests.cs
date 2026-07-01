using System.Linq;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ProviderContractTests
{
    private sealed class FakeProvider : IZakladniChartProvider
    {
        public string Key => "fake";
        public string SectionKey => "zakladni";
        public int Order => 10;
        public ChartData Build(ZakladniDataset dataset)
            => ChartData.ForStatCards("fake", "Fake", new[] { new StatCard("Záznamů", dataset.Records.Count.ToString()) });
    }

    [Fact]
    public void Provider_BuildsChartFromDataset()
    {
        var dataset = new ZakladniDataset
        {
            Obdobi = Obdobi.Rok(2025),
            Records = new[]
            {
                new DatasetRecord(1, SubsystemId: 7, StavId: 1, DatumZalozeni: new System.DateTime(2025, 2, 1), DatumUkonceni: new System.DateTime(2025, 6, 1)),
                new DatasetRecord(2, SubsystemId: 7, StavId: 2, DatumZalozeni: new System.DateTime(2025, 3, 1), DatumUkonceni: new System.DateTime(2025, 7, 1))
            }
        };

        IZakladniChartProvider provider = new FakeProvider();
        var chart = provider.Build(dataset);

        chart.Key.Should().Be("fake");
        chart.Stats.Single().Value.Should().Be("2");
        provider.SectionKey.Should().Be("zakladni");
        provider.Order.Should().Be(10);
    }
}
