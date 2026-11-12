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

    [Fact]
    public void IsEmpty_pro_nulove_serie()
    {
        ChartData Graf(params double[] hodnoty) => ChartData.ForSeries("k", ChartKind.Bar, "T", new[] { "A", "B" },
            new[] { new ChartSeries("Počet", hodnoty) });

        Graf(0, 0).IsEmpty.Should().BeTrue();
        Graf(0, 3).IsEmpty.Should().BeFalse();
        ChartData.ForSeries("k", ChartKind.Donut, "T", [], []).IsEmpty.Should().BeTrue("graf bez sérií nemá co kreslit");
    }

    [Fact]
    public void IsEmpty_pro_karty_podle_poctu_karet()
    {
        ChartData.ForStatCards("k", "T", []).IsEmpty.Should().BeTrue();
        ChartData.ForKpi("k", "T", new[] { new StatCard("Otevřené úkoly", "0") }).IsEmpty.Should().BeFalse("nula je hodnota, ne prázdno");
    }

    [Theory]
    [InlineData(5.0, TrendPolarity.HigherIsBetter, true, false)]
    [InlineData(-5.0, TrendPolarity.HigherIsBetter, false, true)]
    [InlineData(5.0, TrendPolarity.LowerIsBetter, false, true)]
    [InlineData(-5.0, TrendPolarity.LowerIsBetter, true, false)]
    [InlineData(0.0, TrendPolarity.HigherIsBetter, false, false)]
    [InlineData(0.0, TrendPolarity.LowerIsBetter, false, false)]
    public void CardTrend_JeLepsi_podle_polarity(double delta, TrendPolarity polarita, bool lepsi, bool horsi)
    {
        var trend = new CardTrend(delta, "x", polarita);

        trend.JeLepsi.Should().Be(lepsi);
        trend.JeHorsi.Should().Be(horsi);
    }

    [Fact]
    public void Vychozi_hodnoty_novych_poli()
    {
        var data = ChartData.ForSeries("k", ChartKind.Bar, "T", new[] { "A" }, new[] { new ChartSeries("S", new double[] { 1 }) });

        data.Span.Should().Be(ChartSpan.Half);
        data.Unit.Should().BeNull();
        data.EmptyMessage.Should().BeNull();
        data.CategoryColorTokens.Should().BeEmpty();
    }
}
