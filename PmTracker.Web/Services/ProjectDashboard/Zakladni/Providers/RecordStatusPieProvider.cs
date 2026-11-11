namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Průběh práce": prstenec stavů úkolů (Nezahájeno / Rozpracováno / Hotovo / Zrušeno)
/// se součtem uprostřed (spec 2026-10-15 §11.2, vizuál 8). Hotovo nese stavovou barvu, ostatní neutrály a modrou.
/// </summary>
public sealed class RecordStatusPieProvider : IZakladniChartProvider
{
    public string Key => "record-status-pie";
    public string SectionKey => "prubeh";
    public int Order => 20;

    public ChartData Build(ZakladniDataset ds)
    {
        var statesById = ds.Stavy.ToDictionary(s => s.Id);
        var countByBucket = ds.Ukoly
            .GroupBy(r => ZakladniStatusMapper.Bucket(r, statesById))
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var cats = ZakladniStatusMapper.Buckets.Select(ZakladniStatusMapper.Label).ToList();
        var vals = ZakladniStatusMapper.Buckets.Select(b => countByBucket.GetValueOrDefault(b)).ToList();

        var celkem = vals.Sum();
        var hotovo = countByBucket.GetValueOrDefault(StatusBucket.Hotovo);
        return ChartData.ForSeries(Key, ChartKind.Donut, "Stav úkolů", cats,
            new[] { new ChartSeries("Úkolů", vals) },
            insight: celkem > 0 ? $"Hotovo {DeadlineDiscipline.Pct((int)hotovo, (int)celkem).ToString("0.0", UkolyMetriky.Cs)} % úkolů." : null) with
        {
            CategoryColorTokens = ZakladniStatusMapper.Buckets.Select(ZakladniStatusMapper.ColorToken).ToList(),
            EmptyMessage = "Projekt zatím nemá žádné úkoly.",
        };
    }
}
