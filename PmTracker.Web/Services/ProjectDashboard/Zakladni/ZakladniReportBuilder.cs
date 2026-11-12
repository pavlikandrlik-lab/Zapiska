namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Načte sdílený dataset jedním průchodem a projede všechny zaregistrované
/// chart-providery. Výsledné <see cref="ChartData"/> poskládá do sekcí.
/// Nula providerů → prázdný report.
/// </summary>
public sealed class ZakladniReportBuilder
{
    /// <summary>Pořadí sekcí Statistik (spec 2026-10-15 §11.1); neznámé sekce jdou za ně podle pořadí providerů.</summary>
    public static readonly IReadOnlyList<string> PoradiSekci = ["prehled", "prubeh", "terminy", "lide", "servicedesk"];

    public const string ServiceDeskSekce = "servicedesk";

    private readonly IZakladniDatasetLoader _loader;
    private readonly IReadOnlyList<IZakladniChartProvider> _providers;

    public ZakladniReportBuilder(IZakladniDatasetLoader loader, IEnumerable<IZakladniChartProvider> providers)
    {
        _loader = loader;
        _providers = providers.ToList();
    }

    public async Task<ZakladniReportViewModel> BuildAsync(int projektId, Obdobi obdobi, CancellationToken ct)
    {
        var dataset = await _loader.LoadAsync(projektId, obdobi, ct);

        var sections = _providers
            .GroupBy(p => p.SectionKey)
            .Select(g => new
            {
                Key = g.Key,
                MinOrder = g.Min(p => p.Order),
                Charts = g.OrderBy(p => p.Order)
                    .Select(p => p.Build(dataset))
                    .ToList()
            })
            .OrderBy(s => PoradiSekci.Contains(s.Key) ? PoradiSekci.ToList().IndexOf(s.Key) : int.MaxValue)
            .ThenBy(s => s.MinOrder)
            .Select(s => new ReportSection(s.Key, s.Key == ServiceDeskSekce && InformacniKarta(dataset) is { } karta ? [karta] : s.Charts))
            .ToList();

        return new ZakladniReportViewModel(projektId, dataset.ProjektNazev, obdobi, sections);
    }

    /// <summary>
    /// Sekce ServiceDesku bez dat = jedna informační karta místo prázdných grafů (spec 2026-10-15 §11.2, §13).
    /// null = data jsou, kreslí se grafy.
    /// </summary>
    private static ChartData? InformacniKarta(ZakladniDataset ds)
    {
        if (ds.ServiceDesk is { Prodleni.Stav: PmTracker.Web.Services.ServiceDesk.Prodleni.ProdleniStavDat.Ok })
        {
            return null;
        }

        var karta = ChartData.ForStatCards("sd-info", "ServiceDesk a dodavatelé", []);
        if (ds.ServiceDesk is not null)
        {
            return karta with { EmptyMessage = "ServiceDesk teď neodpovídá. Zkuste to za chvíli, ostatní statistiky platí." };
        }
        return ds.ServiceDeskInfoSystemId is null
            ? karta with
            {
                EmptyMessage = "Projekt není napojen na informační systém ve ServiceDesku, proto tu nejsou tickety, prodlení dodavatelů ani kalkulace.",
                ActionUrl = "/Projekty",
                ActionText = "Nastavit napojení",
            }
            : karta with { EmptyMessage = "Aplikace nemá přístup k databázi ServiceDesku. Zapnout ho může správce aplikace." };
    }
}
