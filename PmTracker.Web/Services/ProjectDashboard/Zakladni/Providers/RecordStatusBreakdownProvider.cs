namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Průběh práce": skládaný sloupec stavů úkolů per subsystém přes celou šířku — kde se práce hromadí
/// (spec 2026-10-15 §11.2, vizuál 9).
/// </summary>
public sealed class RecordStatusBreakdownProvider : IZakladniChartProvider
{
    public string Key => "record-status-breakdown";
    public string SectionKey => "prubeh";
    public int Order => 30;

    public ChartData Build(ZakladniDataset ds)
    {
        var statesById = ds.Stavy.ToDictionary(s => s.Id);

        // countByBucket[bucket][subsystemId] = počet
        var countByBucket = ds.Ukoly
            .GroupBy(r => ZakladniStatusMapper.Bucket(r, statesById))
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.SubsystemId).ToDictionary(sg => sg.Key, sg => (double)sg.Count()));

        var cats = ds.Subsystemy.Select(s => s.Nazev).ToList();

        var series = ZakladniStatusMapper.Buckets.Select(bucket =>
        {
            var perSubsystem = countByBucket.GetValueOrDefault(bucket) ?? new Dictionary<int, double>();
            var vals = ds.Subsystemy.Select(s => perSubsystem.GetValueOrDefault(s.Id)).ToList();
            return new ChartSeries(ZakladniStatusMapper.Label(bucket), vals, ZakladniStatusMapper.ColorToken(bucket));
        }).ToList();

        return ChartData.ForSeries(Key, ChartKind.StackedBar, "Stav úkolů podle subsystému", cats, series,
            insight: "Kolik je v každém subsystému hotovo, rozpracováno a nezahájeno.") with
        {
            Span = ChartSpan.Full,
            EmptyMessage = "Projekt zatím nemá žádné úkoly.",
        };
    }
}
