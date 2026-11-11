using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// Každý zaregistrovaný provider na prázdném datasetu (nový projekt, období bez dat) nespadne a místo
/// prázdných os má prázdný stav s textem (plán Review Focus 5).
/// </summary>
public sealed class AllProvidersEmptyDatasetTests
{
    public static IEnumerable<object[]> Providery()
    {
        var typy = typeof(IZakladniChartProvider).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IZakladniChartProvider).IsAssignableFrom(t))
            .OrderBy(t => t.Name);
        return typy.Select(t => new object[] { t.Name });
    }

    [Theory]
    [MemberData(nameof(Providery))]
    public void Vsechny_providery_na_prazdnem_datasetu_nespadnou(string nazev)
    {
        var typ = typeof(IZakladniChartProvider).Assembly.GetTypes().Single(t => t.Name == nazev);
        var provider = (IZakladniChartProvider)ActivatorUtilities.CreateInstance(new ServiceCollection().BuildServiceProvider(), typ);
        var ds = new ZakladniDataset { Obdobi = Obdobi.Kvartal(2026, 1), Dnes = new DateTime(2026, 3, 31) };

        var data = provider.Build(ds);

        data.Key.Should().Be(provider.Key);
        (data.IsEmpty || data.Stats.Count > 0).Should().BeTrue();
        if (data.Kind is not (ChartKind.Kpi or ChartKind.StatCards))
        {
            data.IsEmpty.Should().BeTrue();
            data.EmptyMessage.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Kazdy_provider_je_zaregistrovany_a_ma_znamou_sekci()
    {
        using var sp = new ServiceCollection().AddZakladniReportProviders().BuildServiceProvider();
        using var scope = sp.CreateScope();
        var registrovane = scope.ServiceProvider.GetServices<IZakladniChartProvider>().ToList();

        Providery().Select(p => (string)p[0]).Should().BeEquivalentTo(registrovane.Select(p => p.GetType().Name));
        registrovane.Select(p => p.SectionKey).Distinct()
            .Should().BeSubsetOf(ZakladniReportBuilder.PoradiSekci);
    }
}
