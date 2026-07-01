namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „termíny": % dodržení PŮVODNÍHO (prvního plánovaného) termínu per subsystém
/// (z dokončených záznamů). Vlastní graf (vlastní osa Y v %).
/// </summary>
public sealed class DeadlineOriginalCompliancePerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "deadline-compliance-puvodni";
    public string SectionKey => "terminy";
    public int Order => 30;

    public ChartData Build(ZakladniDataset ds)
    {
        var bySubsystem = DeadlineDiscipline.PerRecord(ds)
            .Where(x => x.JeDokonceno)
            .GroupBy(x => x.SubsystemId)
            .ToDictionary(g => g.Key, g => DeadlineDiscipline.Pct(g.Count(x => x.DodrzelPuvodni), g.Count()));

        var vals = ds.Subsystemy.Select(s => bySubsystem.GetValueOrDefault(s.Id)).ToList();

        return ChartData.ForSeries(Key, ChartKind.Bar, "Dodržení původního termínu (%)",
            ds.Subsystemy.Select(s => s.Nazev).ToList(),
            new[] { new ChartSeries("% včas", vals) },
            insight: "Podíl záznamů dokončených do prvního plánovaného termínu.");
    }
}
