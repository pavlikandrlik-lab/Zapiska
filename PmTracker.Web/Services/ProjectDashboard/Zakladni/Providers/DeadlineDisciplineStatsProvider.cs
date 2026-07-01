using System.Globalization;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „termíny": souhrnné stat-karty termínové disciplíny — dodržel původní/poslední termín
/// (% z dokončených) a ø posun (dní) / ø počet posunů (z posunutých záznamů).
/// </summary>
public sealed class DeadlineDisciplineStatsProvider : IZakladniChartProvider
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    public string Key => "deadline-discipline-stats";
    public string SectionKey => "terminy";
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var perRecord = DeadlineDiscipline.PerRecord(ds);

        var dokoncene = perRecord.Where(x => x.JeDokonceno).ToList();
        var posunute = perRecord.Where(x => x.MaPosun).ToList();

        var dodrzelPuvodni = DeadlineDiscipline.Pct(dokoncene.Count(x => x.DodrzelPuvodni), dokoncene.Count);
        var dodrzelPosledni = DeadlineDiscipline.Pct(dokoncene.Count(x => x.DodrzelPosledni), dokoncene.Count);
        var avgPosunDni = posunute.Count == 0 ? 0d : posunute.Average(x => x.DnyPosunu);
        var avgPocet = posunute.Count == 0 ? 0d : posunute.Average(x => x.PocetPosunu);

        var stats = new[]
        {
            new StatCard("Dodržel původní termín", $"{dodrzelPuvodni.ToString("0.0", Cs)} %", "z dokončených záznamů"),
            new StatCard("Dodržel poslední termín", $"{dodrzelPosledni.ToString("0.0", Cs)} %", "z dokončených záznamů"),
            new StatCard("ø posun (dní)", avgPosunDni.ToString("0.0", Cs), "z posunutých záznamů"),
            new StatCard("ø počet posunů", avgPocet.ToString("0.0", Cs), "z posunutých záznamů")
        };

        return ChartData.ForStatCards(Key, "Termínová disciplína — souhrn", stats,
            insight: "Drží se termíny vůči plánu i realitě a jak moc se posouvalo.");
    }
}
