using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ChartDataTests
{
    [Fact]
    public void ForSeries_SetsKindCategoriesAndSeries()
    {
        var data = ChartData.ForSeries(
            key: "records-per-subsystem",
            kind: ChartKind.Bar,
            title: "Záznamy per subsystém",
            categories: new[] { "GESTOR", "INTEGRACE" },
            series: new[] { new ChartSeries("Počet", new double[] { 3, 5 }) },
            insight: "GESTOR nese nejvíc práce");

        data.Key.Should().Be("records-per-subsystem");
        data.Kind.Should().Be(ChartKind.Bar);
        data.Categories.Should().Equal("GESTOR", "INTEGRACE");
        data.Series.Should().ContainSingle();
        data.Series[0].Values.Should().Equal(3, 5);
        data.Insight.Should().Be("GESTOR nese nejvíc práce");
        data.Stats.Should().BeEmpty();
    }

    [Fact]
    public void ForStatCards_SetsKindAndStats()
    {
        var data = ChartData.ForStatCards(
            key: "vyjadreni-summary",
            title: "Vyjádření",
            stats: new[] { new StatCard("Celkem", "42"), new StatCard("Ø na záznam", "1,8") });

        data.Kind.Should().Be(ChartKind.StatCards);
        data.Stats.Should().HaveCount(2);
        data.Stats[0].Value.Should().Be("42");
        data.Series.Should().BeEmpty();
        data.Categories.Should().BeEmpty();
    }
}
