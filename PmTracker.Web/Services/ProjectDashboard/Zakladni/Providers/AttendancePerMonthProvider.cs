using static PmTracker.Web.Services.ProjectDashboard.Zakladni.UkolyMetriky;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Lidé a spolupráce": účast na jednáních po měsících — podíl pozvaných ve stavu PRESENT
/// (spec 2026-10-15 §11.2, vizuál 14). Insight srovnává s předchozím obdobím.
/// </summary>
public sealed class AttendancePerMonthProvider : IZakladniChartProvider
{
    public string Key => "attendance-per-month";
    public string SectionKey => "lide";
    public int Order => 20;

    public ChartData Build(ZakladniDataset ds)
    {
        var mesice = Mesice(ds.Obdobi, ds.Dnes);
        var ucastiPodleJednani = ds.Ucasti.ToLookup(u => u.JednaniId);

        IReadOnlyList<DatasetUcast> Ucasti(Obdobi o) => ds.Jednani
            .Where(j => j.Datum >= o.Start && j.Datum <= o.End && j.Datum <= ds.Dnes)
            .SelectMany(j => ucastiPodleJednani[j.Id])
            .ToList();

        var hodnoty = mesice.Select(m =>
        {
            var ucasti = ds.Jednani
                .Where(j => VMesici(j.Datum, m) && j.Datum <= ds.Dnes)
                .SelectMany(j => ucastiPodleJednani[j.Id])
                .ToList();
            return DeadlineDiscipline.Pct(ucasti.Count(u => u.Pritomen), ucasti.Count);
        }).ToList();

        var ted = Ucasti(ds.Obdobi);
        var minule = Ucasti(ds.Obdobi.Predchozi());
        string? insight = null;
        if (ted.Count > 0)
        {
            var pct = DeadlineDiscipline.Pct(ted.Count(u => u.Pritomen), ted.Count);
            insight = $"Za období přítomno {Cislo(pct)} % pozvaných";
            if (minule.Count > 0)
            {
                var pred = DeadlineDiscipline.Pct(minule.Count(u => u.Pritomen), minule.Count);
                insight += $" (předchozí období {Cislo(pred)} %)";
            }
            insight += ".";
        }

        return ChartData.ForSeries(Key, ChartKind.Bar, "Účast na jednáních",
            mesice.Select(Mesic).ToList(),
            [new ChartSeries("Přítomno", hodnoty)],
            insight: insight,
            note: "0 % znamená, že v měsíci nebylo žádné jednání s evidovanou účastí.") with
        {
            Unit = "%",
            EmptyMessage = "V období neproběhlo žádné jednání s evidovanou účastí.",
        };
    }
}
