namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Termíny": dodržení původního a posledního termínu per subsystém v jednom seskupeném grafu
/// (spec 2026-10-15 §11.2, vizuál 10). Obě série jsou % z uzavřených úkolů → jedna osa.
/// </summary>
public sealed class DeadlineCompliancePerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "deadline-compliance-per-subsystem";
    public string SectionKey => "terminy";
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var bySubsystem = DeadlineDiscipline.PerRecord(ds)
            .Where(x => x.JeDokonceno)
            .GroupBy(x => x.SubsystemId)
            .ToDictionary(g => g.Key, g => (
                Puvodni: DeadlineDiscipline.Pct(g.Count(x => x.DodrzelPuvodni), g.Count()),
                Posledni: DeadlineDiscipline.Pct(g.Count(x => x.DodrzelPosledni), g.Count())));

        var puvodni = ds.Subsystemy.Select(s => bySubsystem.TryGetValue(s.Id, out var v) ? v.Puvodni : 0d).ToList();
        var posledni = ds.Subsystemy.Select(s => bySubsystem.TryGetValue(s.Id, out var v) ? v.Posledni : 0d).ToList();

        return ChartData.ForSeries(Key, ChartKind.Bar, "Dodržení termínů podle subsystému",
            ds.Subsystemy.Select(s => s.Nazev).ToList(),
            [new ChartSeries("Původní termín", puvodni, "--app-chart-2"), new ChartSeries("Poslední termín", posledni, "--app-chart-1")],
            insight: "Rozdíl sloupců = kolik plnění zachránil posun termínu.",
            note: "Podíl uzavřených úkolů, které skončily do původního (prvního plánovaného) a do posledního platného termínu.") with
        {
            Unit = "%",
            MaData = bySubsystem.Count > 0,
            EmptyMessage = "Zatím se neuzavřel žádný úkol.",
        };
    }
}
