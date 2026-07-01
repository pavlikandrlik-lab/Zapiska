namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „termíny": % dodržení POSLEDNÍHO platného termínu per subsystém (z dokončených záznamů).
/// Vlastní graf (vlastní osa Y v %) — metriky termínové disciplíny se nesměšují.
/// </summary>
public sealed class DeadlineCompliancePerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "deadline-compliance-posledni";
    public string SectionKey => "terminy";
    public int Order => 20;

    public ChartData Build(ZakladniDataset ds)
    {
        var bySubsystem = DeadlineDiscipline.PerRecord(ds)
            .Where(x => x.JeDokonceno)
            .GroupBy(x => x.SubsystemId)
            .ToDictionary(g => g.Key, g => DeadlineDiscipline.Pct(g.Count(x => x.DodrzelPosledni), g.Count()));

        var vals = ds.Subsystemy.Select(s => bySubsystem.GetValueOrDefault(s.Id)).ToList();

        return ChartData.ForSeries(Key, ChartKind.Bar, "Dodržení posledního termínu (%)",
            ds.Subsystemy.Select(s => s.Nazev).ToList(),
            new[] { new ChartSeries("% včas", vals) },
            insight: "Podíl záznamů dokončených do aktuálního termínu.");
    }
}
