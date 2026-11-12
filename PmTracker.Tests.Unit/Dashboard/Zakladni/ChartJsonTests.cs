using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

/// <summary>
/// 2026-06-30: ECharts renderer čte <see cref="ChartData"/> z JSON atributu ve view.
/// Serializer musí emitovat <c>kind</c> jako string (ne číslo enumu) a camelCase klíče.
/// </summary>
public sealed class ChartJsonTests
{
    [Fact]
    public void Serialize_emits_kind_as_string_and_series()
    {
        var data = ChartData.ForSeries("k", ChartKind.Bar, "T",
            new[] { "A", "B" }, new[] { new ChartSeries("Počet", new double[] { 1, 2 }) });

        var json = ChartJson.Serialize(data);

        json.Should().Contain("\"kind\":\"Bar\"");
        json.Should().Contain("\"categories\":[\"A\",\"B\"]");
        json.Should().Contain("\"values\":[1,2]");
    }

    [Fact]
    public void Serialize_emits_stats_for_statcards()
    {
        var data = ChartData.ForStatCards("k", "T", new[] { new StatCard("Celkem", "42") });

        var json = ChartJson.Serialize(data);

        json.Should().Contain("\"kind\":\"StatCards\"");
        json.Should().Contain("\"stats\":[");
        json.Should().Contain("\"label\":\"Celkem\"");
        json.Should().Contain("\"value\":\"42\"");
    }

    [Fact]
    public void ChartJson_serializuje_span_unit_trend()
    {
        var data = ChartData.ForKpi("kpi", "Přehled", new[]
        {
            new StatCard("Dokončeno", "63", Trend: new CardTrend(12, "o 12 % víc", TrendPolarity.HigherIsBetter),
                Status: CardStatus.Good, Progress: 0.64)
        }) with { Span = ChartSpan.Full, Unit = "%" };

        var json = ChartJson.Serialize(data);

        json.Should().Contain("\"kind\":\"Kpi\"");
        json.Should().Contain("\"span\":\"Full\"");
        json.Should().Contain("\"unit\":\"%\"");
        json.Should().Contain("\"goodWhen\":\"HigherIsBetter\"");
        json.Should().Contain("\"status\":\"Good\"");
        json.Should().Contain("\"progress\":0.64");
    }

    [Fact]
    public void ChartJson_serializuje_barevne_tokeny_serii_i_kategorii()
    {
        var data = ChartData.ForSeries("k", ChartKind.Donut, "T", new[] { "Hotovo", "Zrušeno" },
            new[] { new ChartSeries("Počet", new double[] { 3, 1 }, "--app-chart-1") })
            with { CategoryColorTokens = new[] { "--background-status-success", "--app-chart-neutral-strong" } };

        var json = ChartJson.Serialize(data);

        json.Should().Contain("\"kind\":\"Donut\"");
        json.Should().Contain("\"colorToken\":\"--app-chart-1\"");
        json.Should().Contain("\"categoryColorTokens\":[\"--background-status-success\",\"--app-chart-neutral-strong\"]");
    }
}
