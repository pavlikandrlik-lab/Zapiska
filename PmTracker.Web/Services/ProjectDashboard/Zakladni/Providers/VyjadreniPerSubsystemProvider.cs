namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „vyjádření": počet vyjádření per subsystém (vyjádření → záznam → subsystém). Kde se komunikuje.
/// </summary>
public sealed class VyjadreniPerSubsystemProvider : IZakladniChartProvider
{
    public string Key => "vyjadreni-per-subsystem";
    public string SectionKey => "vyjadreni";
    public int Order => 20;

    public ChartData Build(ZakladniDataset ds)
    {
        var subsystemByRecord = ds.Records.ToDictionary(r => r.Id, r => r.SubsystemId);

        var countBySubsystem = ds.Vyjadreni
            .Where(v => subsystemByRecord.ContainsKey(v.ZaznamId))
            .GroupBy(v => subsystemByRecord[v.ZaznamId])
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var cats = ds.Subsystemy.Select(s => s.Nazev).ToList();
        var vals = ds.Subsystemy.Select(s => countBySubsystem.GetValueOrDefault(s.Id)).ToList();

        return ChartData.ForSeries(Key, ChartKind.Bar, "Vyjádření per subsystém", cats,
            new[] { new ChartSeries("Počet", vals) });
    }
}
