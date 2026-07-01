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
}
