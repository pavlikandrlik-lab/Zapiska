namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „záznamy": koláč stavového rozpadu za celý projekt (Nezahájeno / Rozpracováno /
/// Hotovo / Zrušeno). Kolik je hotovo vs rozděláno vs nezačato.
/// </summary>
public sealed class RecordStatusPieProvider : IZakladniChartProvider
{
    public string Key => "record-status-pie";
    public string SectionKey => "zaznamy";
    public int Order => 20;

    public ChartData Build(ZakladniDataset ds)
    {
        var statesById = ds.Stavy.ToDictionary(s => s.Id);
        var countByBucket = ds.Records
            .GroupBy(r => ZakladniStatusMapper.Bucket(r, statesById))
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var cats = ZakladniStatusMapper.Buckets.Select(ZakladniStatusMapper.Label).ToList();
        var vals = ZakladniStatusMapper.Buckets.Select(b => countByBucket.GetValueOrDefault(b)).ToList();

        return ChartData.ForSeries(Key, ChartKind.Pie, "Stavový rozpad záznamů", cats,
            new[] { new ChartSeries("Počet", vals) },
            insight: "Podíl hotových, rozpracovaných, nezahájených a zrušených.");
    }
}
