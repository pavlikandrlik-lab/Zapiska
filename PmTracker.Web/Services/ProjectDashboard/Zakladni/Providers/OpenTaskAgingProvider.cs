using static PmTracker.Web.Services.ProjectDashboard.Zakladni.UkolyMetriky;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Termíny": stáří otevřených úkolů k dnešku v pěti koších (spec 2026-10-15 §11.2, vizuál 12).
/// </summary>
public sealed class OpenTaskAgingProvider : IZakladniChartProvider
{
    private static readonly (string Popisek, int DoDni)[] Kose =
    [
        ("0–30 dní", 30), ("31–90 dní", 90), ("91–180 dní", 180), ("181–365 dní", 365), ("víc než rok", int.MaxValue),
    ];

    public string Key => "open-task-aging";
    public string SectionKey => "terminy";
    public int Order => 30;

    public ChartData Build(ZakladniDataset ds)
    {
        var stavy = StavyPodleId(ds);
        var stari = ds.Ukoly
            .Where(r => JeOtevreny(r, stavy))
            .Select(r => Math.Max(0, (ds.Dnes.Date - r.DatumZalozeni.Date).Days))
            .ToList();

        var hodnoty = new double[Kose.Length];
        foreach (var dni in stari)
        {
            hodnoty[Array.FindIndex(Kose, k => dni <= k.DoDni)]++;
        }

        var starsiNezRok = hodnoty[^1];
        return ChartData.ForSeries(Key, ChartKind.Bar, "Stáří otevřených úkolů",
            Kose.Select(k => k.Popisek).ToList(),
            [new ChartSeries("Otevřené úkoly", hodnoty)],
            insight: stari.Count == 0 ? null
                : starsiNezRok > 0 ? $"{starsiNezRok.ToString(Cs)} otevřených úkolů je starších než rok."
                : $"Nejstarší otevřený úkol vznikl před {Dni(stari.Max())}.",
            note: "Stáří = dnes minus datum založení.") with
        {
            EmptyMessage = "Žádný úkol není otevřený.",
        };
    }
}
