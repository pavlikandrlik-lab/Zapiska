using static PmTracker.Web.Services.ProjectDashboard.Zakladni.UkolyMetriky;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Průběh práce": burn-up — kumulativně založené vs. dokončené úkoly po měsících období
/// (spec 2026-10-15 §11.2, vizuál 7). Jedna osa, dvě čáry; rozevírání = přibývá víc, než se stíhá.
/// </summary>
public sealed class BurnUpProvider : IZakladniChartProvider
{
    public string Key => "burn-up";
    public string SectionKey => "prubeh";
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var stavy = StavyPodleId(ds);
        var ukoly = ds.Ukoly.ToList();
        var mesice = Mesice(ds.Obdobi, ds.Dnes);

        var zalozene = ukoly.Where(r => r.DatumZalozeni >= ds.Obdobi.Start && r.DatumZalozeni <= ds.Obdobi.End).ToList();
        var hotove = ukoly.Where(r => JeHotovyV(r, stavy, ds.Obdobi)).ToList();

        double zalozenoCelkem = 0, hotovoCelkem = 0;
        var zalozenoKum = new List<double>();
        var hotovoKum = new List<double>();
        foreach (var m in mesice)
        {
            zalozenoCelkem += zalozene.Count(r => VMesici(r.DatumZalozeni, m));
            hotovoCelkem += hotove.Count(r => VMesici(r.DatumDokonceni!.Value, m));
            zalozenoKum.Add(zalozenoCelkem);
            hotovoKum.Add(hotovoCelkem);
        }

        var rozdil = zalozenoCelkem - hotovoCelkem;
        string? insight = mesice.Count == 0 ? null
            : rozdil > 0 ? $"Založeno o {rozdil.ToString(Cs)} úkolů víc, než se dokončilo."
            : rozdil < 0 ? $"Dokončeno o {(-rozdil).ToString(Cs)} úkolů víc, než přibylo."
            : "Dokončuje se stejně rychle, jako úkoly přibývají.";

        return ChartData.ForSeries(Key, ChartKind.Line, "Založené a dokončené úkoly (kumulativně)",
            mesice.Select(Mesic).ToList(),
            [new ChartSeries("Založeno", zalozenoKum, "--app-chart-neutral-strong"), new ChartSeries("Dokončeno", hotovoKum, "--app-chart-1")],
            insight: insight,
            note: "Dokončeno = úkoly ve stavu Hotovo s datem dokončení v měsíci, i když vznikly dřív.") with
        {
            EmptyMessage = "V období úkoly nepřibývaly ani se nedokončovaly.",
        };
    }
}
