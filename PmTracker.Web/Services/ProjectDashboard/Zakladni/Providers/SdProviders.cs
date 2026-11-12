using PmTracker.Web.Services.ServiceDesk.Prodleni;
using static PmTracker.Web.Services.ProjectDashboard.Zakladni.UkolyMetriky;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>Otevřené (nearchivované) synchronizované tickety podle typu: u dodavatele / u nás (spec §11.2, vizuál 16).</summary>
public sealed class SdOpenTicketsByTypeProvider : IZakladniChartProvider
{
    private static readonly string[] Typy = ["NES", "PMP", "PNF"];

    public string Key => "sd-open-by-type";
    public string SectionKey => ZakladniReportBuilder.ServiceDeskSekce;
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var otevrene = ds.ServiceDesk?.Prodleni.Otevrene ?? [];
        double Pocet(string typ, bool uDodavatele)
            => otevrene.Count(t => t.UDodavatele == uDodavatele && string.Equals(t.TypZaznamu.Trim(), typ, StringComparison.OrdinalIgnoreCase));

        var uDodavatele = Typy.Select(t => Pocet(t, true)).ToList();
        var uNas = Typy.Select(t => Pocet(t, false)).ToList();
        var celkem = uDodavatele.Sum() + uNas.Sum();

        return ChartData.ForSeries(Key, ChartKind.StackedBar, "Otevřené tickety podle typu", Typy,
            [new ChartSeries("U dodavatele", uDodavatele, "--app-chart-1"), new ChartSeries("U nás", uNas, "--app-chart-neutral")],
            insight: celkem > 0 ? $"U dodavatele leží {uDodavatele.Sum().ToString(Cs)} z {celkem.ToString(Cs)} otevřených ticketů." : null,
            note: "Stav k dnešku, jen tickety synchronizované s dodavatelem.") with
        {
            EmptyMessage = "Informační systém nemá žádné otevřené synchronizované tickety.",
        };
    }
}

/// <summary>Penalizované dny v období podle dodavatele, včetně ticketů už vrácených (spec §11.2, vizuál 17).</summary>
public sealed class SdPenaltyBySupplierProvider : IZakladniChartProvider
{
    public string Key => "sd-penalty-by-supplier";
    public string SectionKey => ZakladniReportBuilder.ServiceDeskSekce;
    public int Order => 20;

    public ChartData Build(ZakladniDataset ds)
    {
        var dodavatele = (ds.ServiceDesk?.Prodleni.Penalizace ?? [])
            .GroupBy(p => string.IsNullOrWhiteSpace(p.Dodavatel) ? "(neuvedeno)" : p.Dodavatel!.Trim())
            .Select(g => (Nazev: g.Key, Dny: g.Sum(p => SdObdobi.DnyV(p, ds.Obdobi).Count())))
            .Where(x => x.Dny > 0)
            .OrderByDescending(x => x.Dny).ThenBy(x => x.Nazev, StringComparer.Create(Cs, ignoreCase: true))
            .ToList();

        return ChartData.ForSeries(Key, ChartKind.HorizontalBar, "Penalizované dny podle dodavatele",
            dodavatele.Select(x => x.Nazev).ToList(),
            [new ChartSeries("Penalizované dny", dodavatele.Select(x => (double)x.Dny).ToList())],
            insight: dodavatele.Count > 0 ? $"Nejvíc {dodavatele[0].Nazev}: {PenaltyReasonFormatter.Dny(dodavatele[0].Dny)}." : null,
            note: "Dny penále, které připadly do období, i u ticketů, které už jsou zpět u nás nebo archivované.") with
        {
            Unit = "dní",
            EmptyMessage = "V období žádný dodavatel neprodléval.",
        };
    }
}

/// <summary>Penalizované dny po měsících období (spec §11.2, vizuál 18).</summary>
public sealed class SdPenaltyPerMonthProvider : IZakladniChartProvider
{
    public string Key => "sd-penalty-per-month";
    public string SectionKey => ZakladniReportBuilder.ServiceDeskSekce;
    public int Order => 30;

    public ChartData Build(ZakladniDataset ds)
    {
        var dny = (ds.ServiceDesk?.Prodleni.Penalizace ?? []).SelectMany(p => SdObdobi.DnyV(p, ds.Obdobi)).ToList();
        var mesice = Mesice(ds.Obdobi, ds.Dnes);

        return ChartData.ForSeries(Key, ChartKind.Bar, "Penalizované dny po měsících",
            mesice.Select(Mesic).ToList(),
            [new ChartSeries("Penalizované dny", mesice.Select(m => (double)dny.Count(d => VMesici(d, m))).ToList())],
            insight: dny.Count > 0 ? $"Za období {PenaltyReasonFormatter.Dny(dny.Count)} penále." : null,
            note: "Součet přes všechny dodavatele a tickety.") with
        {
            Unit = "dní",
            EmptyMessage = "V období žádný dodavatel neprodléval.",
        };
    }
}

/// <summary>Akceptované kalkulace ticketů IS po měsících data akceptace (spec §11.2, vizuál 19).</summary>
public sealed class SdAcceptedCalculationsProvider : IZakladniChartProvider
{
    public string Key => "sd-accepted-calculations";
    public string SectionKey => ZakladniReportBuilder.ServiceDeskSekce;
    public int Order => 40;

    public ChartData Build(ZakladniDataset ds)
    {
        var kalkulace = (ds.ServiceDesk?.Kalkulace ?? [])
            .Where(k => k.DatumAkceptace >= ds.Obdobi.Start && k.DatumAkceptace <= ds.Obdobi.End)
            .ToList();
        var mesice = Mesice(ds.Obdobi, ds.Dnes);
        var celkem = kalkulace.Sum(k => k.Cena);

        return ChartData.ForSeries(Key, ChartKind.Bar, "Akceptované kalkulace po měsících",
            mesice.Select(Mesic).ToList(),
            [new ChartSeries("Akceptováno", mesice.Select(m => (double)kalkulace.Where(k => VMesici(k.DatumAkceptace, m)).Sum(k => k.Cena)).ToList())],
            insight: kalkulace.Count > 0
                ? $"Za období akceptováno {kalkulace.Count.ToString(Cs)} kalkulací za {celkem.ToString("#,##0", Cs)} Kč."
                : null,
            note: "Podle data akceptace; u ticketu platí jeho poslední akceptovaná kalkulace.") with
        {
            Unit = "Kč",
            EmptyMessage = "V období nebyla akceptována žádná kalkulace.",
        };
    }
}

internal static class SdObdobi
{
    public static IEnumerable<DateTime> DnyV(PenalizaceVObdobi p, Obdobi obdobi)
        => p.Dny.Where(d => d >= obdobi.Start && d <= obdobi.End);
}
