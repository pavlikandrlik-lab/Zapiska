namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „termíny": ø posun termínu ve dnech per subsystém (z posunutých záznamů; subsystém
/// bez posunů = 0). Vlastní graf (vlastní osa Y ve dnech) — jiné jednotky než % dodržení.
/// </summary>
public sealed class DeadlineAvgShiftDaysPerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "deadline-avg-shift-days";
    public string SectionKey => "terminy";
    public int Order => 40;

    public ChartData Build(ZakladniDataset ds)
    {
        var bySubsystem = DeadlineDiscipline.PerRecord(ds)
            .Where(x => x.MaPosun)
            .GroupBy(x => x.SubsystemId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Average(x => (double)x.DnyPosunu), 1));

        var vals = ds.Subsystemy.Select(s => bySubsystem.GetValueOrDefault(s.Id)).ToList();

        return ChartData.ForSeries(Key, ChartKind.Bar, "ø posun termínu (dní)",
            ds.Subsystemy.Select(s => s.Nazev).ToList(),
            new[] { new ChartSeries("Dní", vals) },
            insight: "O kolik dní se v průměru posouval termín.");
    }
}
