using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ZakladniReportBuilderTests
{
    private sealed class StubLoader : IZakladniDatasetLoader
    {
        public Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct)
            => Task.FromResult(new ZakladniDataset { Obdobi = obdobi });
    }

    private sealed class StubProvider(string key, string section, int order) : IZakladniChartProvider
    {
        public string Key => key;
        public string SectionKey => section;
        public int Order => order;
        public ChartData Build(ZakladniDataset dataset)
            => ChartData.ForStatCards(key, key, new[] { new StatCard("x", "1") });
    }

    [Fact]
    public async Task BuildAsync_NoProviders_ProducesEmptyReport()
    {
        var builder = new ZakladniReportBuilder(new StubLoader(), Enumerable.Empty<IZakladniChartProvider>());
        var report = await builder.BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        report.ProjektId.Should().Be(1);
        report.Obdobi.Label.Should().Be("2025");
        report.Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildAsync_GroupsBySectionAndOrdersByOrder()
    {
        var providers = new IZakladniChartProvider[]
        {
            new StubProvider("b", "zakladni", 20),
            new StubProvider("a", "zakladni", 10),
            new StubProvider("c", "termin", 5)
        };
        var builder = new ZakladniReportBuilder(new StubLoader(), providers);
        var report = await builder.BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        // sekce „termin" má min Order 5 → první; „zakladni" min Order 10 → druhá.
        report.Sections.Select(s => s.Key).Should().Equal("termin", "zakladni");
        // grafy v „zakladni" dle Order: a(10) před b(20).
        report.Sections.Last().Charts.Select(c => c.Key).Should().Equal("a", "b");
    }
}
