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
    private sealed class StubLoader(ServiceDeskSlice? sd = null, int? isId = null) : IZakladniDatasetLoader
    {
        public Task<ZakladniDataset> LoadAsync(int projektId, Obdobi obdobi, CancellationToken ct)
            => Task.FromResult(new ZakladniDataset { Obdobi = obdobi, ServiceDesk = sd, ServiceDeskInfoSystemId = isId });
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

    private static IZakladniChartProvider[] SdSekce() =>
    [
        new StubProvider("prehled-kpi", "prehled", 10),
        new StubProvider("sd-a", "servicedesk", 10),
        new StubProvider("sd-b", "servicedesk", 20),
    ];

    [Fact]
    public async Task Builder_bez_sd_jedna_informacni_karta()
    {
        var report = await new ZakladniReportBuilder(new StubLoader(), SdSekce()).BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        var sekce = report.Sections.Single(s => s.Key == "servicedesk");
        var karta = sekce.Charts.Should().ContainSingle().Subject;
        karta.IsEmpty.Should().BeTrue();
        karta.EmptyMessage.Should().Contain("není napojen");
        karta.ActionUrl.Should().Be("/Projekty");
    }

    [Fact]
    public async Task Builder_is_bez_databaze_servicedesku_karta_bez_odkazu()
    {
        var report = await new ZakladniReportBuilder(new StubLoader(isId: 1), SdSekce()).BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        var karta = report.Sections.Single(s => s.Key == "servicedesk").Charts.Single();
        karta.EmptyMessage.Should().Contain("správce");
        karta.ActionUrl.Should().BeNull();
    }

    [Fact]
    public async Task Builder_sd_nedostupny_chybova_karta()
    {
        var sd = new ServiceDeskSlice(PmTracker.Web.Services.ServiceDesk.Prodleni.ProdleniPrehled.Prazdny(
            PmTracker.Web.Services.ServiceDesk.Prodleni.ProdleniStavDat.Nedostupne, 1, "FIS"), null, []);
        var report = await new ZakladniReportBuilder(new StubLoader(sd, 1), SdSekce()).BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        var karta = report.Sections.Single(s => s.Key == "servicedesk").Charts.Single();
        karta.EmptyMessage.Should().Contain("neodpovídá");
        report.Sections.Single(s => s.Key == "prehled").Charts.Should().ContainSingle("ostatní sekce se vykreslí dál");
    }

    [Fact]
    public async Task Builder_se_sd_vola_providery()
    {
        var sd = new ServiceDeskSlice(PmTracker.Web.Services.ServiceDesk.Prodleni.ProdleniPrehled.Prazdny(
            PmTracker.Web.Services.ServiceDesk.Prodleni.ProdleniStavDat.Ok, 1, "FIS"), null, []);
        var report = await new ZakladniReportBuilder(new StubLoader(sd, 1), SdSekce()).BuildAsync(1, Obdobi.Rok(2025), CancellationToken.None);

        report.Sections.Select(s => s.Key).Should().Equal("prehled", "servicedesk");
        report.Sections.Last().Charts.Select(c => c.Key).Should().Equal("sd-a", "sd-b");
    }
}
