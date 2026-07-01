using System.Globalization;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „vyjádření": stat-karty — celkem vyjádření za období a ø na záznam. Kolik se komunikuje.
/// </summary>
public sealed class VyjadreniStatsProvider : IZakladniChartProvider
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    public string Key => "vyjadreni-stats";
    public string SectionKey => "vyjadreni";
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var total = ds.Vyjadreni.Count;
        var recordCount = ds.Records.Count;
        var average = recordCount == 0 ? 0d : (double)total / recordCount;

        var stats = new[]
        {
            new StatCard("Celkem vyjádření", total.ToString(CultureInfo.InvariantCulture)),
            new StatCard("ø na záznam", average.ToString("0.0", Cs))
        };

        return ChartData.ForStatCards(Key, "Vyjádření — souhrn", stats,
            insight: "Kolik se v projektu komunikuje.");
    }
}
