using PmTracker.Web.Services.ServiceDesk.Prodleni;
using static PmTracker.Web.Services.ProjectDashboard.Zakladni.UkolyMetriky;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Přehled": KPI řada (spec 2026-10-15 §11.2, vizuály 1–6). Úkolové karty se srovnávají
/// s předchozím obdobím stejné délky; ServiceDesk karty jen s napojením na IS.
/// </summary>
public sealed class PrehledKpiProvider : IZakladniChartProvider
{
    public string Key => "prehled-kpi";
    public string SectionKey => "prehled";
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var stavy = StavyPodleId(ds);
        var ukoly = ds.Ukoly.ToList();
        var predchozi = ds.Obdobi.Predchozi();
        var srovnani = $"než v předchozím období ({predchozi.Label})";

        var karty = new List<StatCard>
        {
            Otevrene(ukoly, stavy, ds.Dnes),
            Dokonceno(ukoly, stavy, ds.Obdobi, predchozi, srovnani),
        };

        var terminy = DeadlineDiscipline.PerRecord(ds);
        var ted = UzavreneV(terminy, ds.Obdobi);
        var minule = UzavreneV(terminy, predchozi);
        karty.Add(Vcasnost(ted, minule, srovnani));
        karty.Add(Posun(ted, minule, srovnani));

        if (ds.ServiceDesk is { } sd)
        {
            karty.Add(Prodleni(sd.Prodleni));
            if (sd.Rozpocet is { Limit: > 0 } rozpocet && rozpocet.Cerpani is not null)
            {
                karty.Add(Rozpocet(rozpocet.Limit!.Value, rozpocet.Cerpani.Value, rozpocet.IsZkratka));
            }
        }

        return ChartData.ForKpi(Key, "Hlavní čísla za období", karty);
    }

    private static StatCard Otevrene(IReadOnlyList<DatasetRecord> ukoly, IReadOnlyDictionary<int, DatasetState> stavy, DateTime dnes)
    {
        var otevrene = ukoly.Where(r => JeOtevreny(r, stavy)).ToList();
        var poTerminu = otevrene.Count(r => JePoTerminu(r, dnes));
        return new StatCard("Otevřené úkoly", otevrene.Count.ToString(Cs),
            Note: poTerminu > 0 ? $"z toho {poTerminu} po termínu ukončení" : "žádný po termínu ukončení",
            Status: poTerminu > 0 ? CardStatus.Warning : CardStatus.Neutral);
    }

    private static StatCard Dokonceno(IReadOnlyList<DatasetRecord> ukoly, IReadOnlyDictionary<int, DatasetState> stavy,
        Obdobi obdobi, Obdobi predchozi, string srovnani)
    {
        var ted = ukoly.Count(r => JeHotovyV(r, stavy, obdobi));
        var minule = ukoly.Count(r => JeHotovyV(r, stavy, predchozi));
        var delta = ted - minule;
        var text = delta == 0 ? $"stejně {srovnani.Replace("než", "jako", StringComparison.Ordinal)}"
            : $"o {Math.Abs(delta).ToString(Cs)} {(delta > 0 ? "víc" : "míň")} {srovnani}";
        return new StatCard("Dokončeno v období", ted.ToString(Cs), Note: "úkolů ve stavu Hotovo",
            Trend: new CardTrend(delta, text, TrendPolarity.HigherIsBetter));
    }

    private static StatCard Vcasnost(IReadOnlyList<RecordDeadline> ted, IReadOnlyList<RecordDeadline> minule, string srovnani)
    {
        if (ted.Count == 0)
        {
            return new StatCard("Včasnost", "—", Note: "v období se žádný úkol neuzavřel");
        }
        var pct = DeadlineDiscipline.Pct(ted.Count(t => t.DodrzelPuvodni), ted.Count);
        CardTrend? trend = null;
        if (minule.Count > 0)
        {
            var delta = Math.Round(pct - DeadlineDiscipline.Pct(minule.Count(t => t.DodrzelPuvodni), minule.Count), 1);
            trend = new CardTrend(delta,
                delta == 0 ? $"stejně jako v předchozím období" : $"o {Cislo(Math.Abs(delta))} b. {(delta > 0 ? "víc" : "míň")} {srovnani}",
                TrendPolarity.HigherIsBetter);
        }
        return new StatCard("Včasnost", $"{Cislo(pct)} %", Note: "uzavřeno do původního termínu", Trend: trend,
            Progress: pct / 100d);
    }

    private static StatCard Posun(IReadOnlyList<RecordDeadline> ted, IReadOnlyList<RecordDeadline> minule, string srovnani)
    {
        var posunute = ted.Where(t => t.MaPosun).ToList();
        if (posunute.Count == 0)
        {
            return new StatCard("Ø posun termínu", "—", Note: ted.Count == 0 ? "v období se žádný úkol neuzavřel" : "žádný uzavřený úkol se neposouval");
        }
        var prumer = Math.Round(posunute.Average(t => (double)t.DnyPosunu), 1);
        CardTrend? trend = null;
        var minulePosunute = minule.Where(t => t.MaPosun).ToList();
        if (minulePosunute.Count > 0)
        {
            var delta = Math.Round(prumer - minulePosunute.Average(t => (double)t.DnyPosunu), 1);
            trend = new CardTrend(delta,
                delta == 0 ? "stejně jako v předchozím období" : $"o {Dni(Math.Abs(delta))} {(delta > 0 ? "víc" : "míň")} {srovnani}",
                TrendPolarity.LowerIsBetter);
        }
        return new StatCard("Ø posun termínu", Dni(prumer),
            Note: $"{posunute.Count.ToString(Cs)} z {ted.Count.ToString(Cs)} uzavřených úkolů posunuto", Trend: trend);
    }

    private static StatCard Prodleni(ProdleniPrehled prehled)
    {
        if (prehled.Stav != ProdleniStavDat.Ok)
        {
            return new StatCard("Tickety v prodlení", "—", Note: "ServiceDesk teď neodpovídá");
        }
        var tickety = prehled.VProdleni.Select(p => p.TicketId).Distinct().Count();
        return new StatCard("Tickety v prodlení", tickety.ToString(Cs),
            Note: tickety > 0 ? $"{PenaltyReasonFormatter.Dny(prehled.SoucetDni)} penále, stav k dnešku" : "stav k dnešku",
            Status: tickety > 0 ? CardStatus.Critical : CardStatus.Good);
    }

    private static StatCard Rozpocet(decimal limit, decimal cerpani, string zkratka)
    {
        var podil = (double)(cerpani / limit);
        var stav = podil >= 1 ? CardStatus.Critical : podil >= 0.9 ? CardStatus.Warning : CardStatus.Neutral;
        return new StatCard("Rozpočet IS", $"{Math.Round(podil * 100).ToString(Cs)} %",
            Note: $"{zkratka}: čerpáno {Kc(cerpani)} z {Kc(limit)}", Status: stav, Progress: podil);
    }

    private static string Kc(decimal castka) => castka.ToString("#,##0", Cs) + " Kč";
}
