namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „záznamy": počet záznamů per subsystém (sloupec). Kdo nese kolik práce.
/// </summary>
public sealed class RecordsPerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "records-per-subsystem";
    public string SectionKey => "zaznamy";
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var countById = ds.Records
            .GroupBy(r => r.SubsystemId)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var cats = ds.Subsystemy.Select(s => s.Nazev).ToList();
        var vals = ds.Subsystemy.Select(s => countById.GetValueOrDefault(s.Id)).ToList();

        return ChartData.ForSeries(Key, ChartKind.Bar, "Záznamy per subsystém", cats,
            new[] { new ChartSeries("Počet", vals) },
            insight: $"Celkem {ds.Records.Count} záznamů za období.");
    }
}
