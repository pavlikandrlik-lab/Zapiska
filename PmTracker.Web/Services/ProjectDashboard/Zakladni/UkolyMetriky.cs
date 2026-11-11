using System.Globalization;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Sdílená pravidla úkolových metrik Statistik (spec 2026-10-15 §11.2), aby KPI a grafy počítaly stejně:
/// otevřený = stav Nezahájeno/Rozpracováno, po termínu = termín (den) před dneškem,
/// dokončený = stav Hotovo s datem dokončení (zrušené úkoly se nepočítají).
/// </summary>
public static class UkolyMetriky
{
    public static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    public static IReadOnlyDictionary<int, DatasetState> StavyPodleId(ZakladniDataset ds)
        => ds.Stavy.ToDictionary(s => s.Id);

    public static bool JeOtevreny(DatasetRecord r, IReadOnlyDictionary<int, DatasetState> stavy)
        => ZakladniStatusMapper.Bucket(r, stavy) is StatusBucket.Nezahajeno or StatusBucket.Rozpracovano;

    public static bool JePoTerminu(DatasetRecord r, DateTime dnes) => r.DatumUkonceni.Date < dnes.Date;

    public static bool JeHotovyV(DatasetRecord r, IReadOnlyDictionary<int, DatasetState> stavy, Obdobi obdobi)
        => r.DatumDokonceni is { } d && d >= obdobi.Start && d <= obdobi.End
           && ZakladniStatusMapper.Bucket(r, stavy) == StatusBucket.Hotovo;

    /// <summary>Úkoly uzavřené v období — základ včasnosti a posunu (stejně jako grafy dodržení termínů).</summary>
    public static IReadOnlyList<RecordDeadline> UzavreneV(IEnumerable<RecordDeadline> terminy, Obdobi obdobi)
        => terminy.Where(t => t.Dokonceni is { } d && d >= obdobi.Start && d <= obdobi.End).ToList();

    /// <summary>Začátky měsíců období, nejdál do dneška — budoucí měsíce by v grafu kreslily vodorovnou čáru.</summary>
    public static IReadOnlyList<DateTime> Mesice(Obdobi obdobi, DateTime dnes)
    {
        var konec = obdobi.End < dnes ? obdobi.End : dnes;
        var mesice = new List<DateTime>();
        for (var m = new DateTime(obdobi.Start.Year, obdobi.Start.Month, 1); m <= konec; m = m.AddMonths(1))
        {
            mesice.Add(m);
        }
        return mesice;
    }

    public static string Mesic(DateTime m) => m.ToString("MM/yyyy", CultureInfo.InvariantCulture);

    public static bool VMesici(DateTime d, DateTime mesic) => d.Year == mesic.Year && d.Month == mesic.Month;

    public static string Cislo(double hodnota, string format = "0.0") => hodnota.ToString(format, Cs);

    /// <summary>„1 den", „2 dny", „5 dní"; desetinné číslo vždy „dne" (9,4 dne).</summary>
    public static string Dni(double pocet)
    {
        if (Math.Abs(pocet - Math.Round(pocet)) > 0.05)
        {
            return $"{Cislo(pocet)} dne";
        }
        var n = (int)Math.Round(pocet);
        var tvar = Math.Abs(n) == 1 ? "den" : Math.Abs(n) is >= 2 and <= 4 ? "dny" : "dní";
        return $"{n.ToString(Cs)} {tvar}";
    }
}
