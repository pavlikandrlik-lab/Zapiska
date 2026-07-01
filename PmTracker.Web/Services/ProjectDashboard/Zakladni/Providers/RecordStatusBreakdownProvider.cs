namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „záznamy": skládaný sloupec stavového rozpadu per subsystém. Kde se práce hromadí
/// v jakém stavu.
/// </summary>
public sealed class RecordStatusBreakdownProvider : IZakladniChartProvider
{
    public string Key => "record-status-breakdown";
    public string SectionKey => "zaznamy";
    public int Order => 30;

    public ChartData Build(ZakladniDataset ds)
    {
        var statesById = ds.Stavy.ToDictionary(s => s.Id);

        // countByBucket[bucket][subsystemId] = počet
        var countByBucket = ds.Records
            .GroupBy(r => ZakladniStatusMapper.Bucket(r, statesById))
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.SubsystemId).ToDictionary(sg => sg.Key, sg => (double)sg.Count()));

        var cats = ds.Subsystemy.Select(s => s.Nazev).ToList();

        var series = ZakladniStatusMapper.Buckets.Select(bucket =>
        {
            var perSubsystem = countByBucket.GetValueOrDefault(bucket) ?? new Dictionary<int, double>();
            var vals = ds.Subsystemy.Select(s => perSubsystem.GetValueOrDefault(s.Id)).ToList();
            return new ChartSeries(ZakladniStatusMapper.Label(bucket), vals);
        }).ToList();

        return ChartData.ForSeries(Key, ChartKind.StackedBar, "Stavový rozpad per subsystém", cats, series,
            insight: "Kolik je v každém subsystému hotovo vs rozděláno vs nezačato.");
    }
}
