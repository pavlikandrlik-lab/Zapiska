namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Termíny": ø posun termínu ve dnech per subsystém (z posunutých úkolů; subsystém
/// bez posunů = 0). Vlastní graf (osa ve dnech) — jiné jednotky než % dodržení (spec §11.2, vizuál 11).
/// </summary>
public sealed class DeadlineAvgShiftDaysPerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "deadline-avg-shift-days";
    public string SectionKey => "terminy";
    public int Order => 20;

    public ChartData Build(ZakladniDataset ds)
    {
        var bySubsystem = DeadlineDiscipline.PerRecord(ds)
            .Where(x => x.MaPosun)
            .GroupBy(x => x.SubsystemId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Average(x => (double)x.DnyPosunu), 1));

        var vals = ds.Subsystemy.Select(s => bySubsystem.GetValueOrDefault(s.Id)).ToList();

        return ChartData.ForSeries(Key, ChartKind.Bar, "Ø posun termínu podle subsystému",
            ds.Subsystemy.Select(s => s.Nazev).ToList(),
            new[] { new ChartSeries("Ø posun", vals) },
            insight: "O kolik dní se v průměru posunul termín posunutých úkolů.") with
        {
            Unit = "dní",
            EmptyMessage = "Žádný úkol se neposouval.",
        };
    }
}
