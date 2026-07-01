namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>
/// Načte sdílený dataset jedním průchodem a projede všechny zaregistrované
/// chart-providery. Výsledné <see cref="ChartData"/> poskládá do sekcí.
/// Nula providerů → prázdný report.
/// </summary>
public sealed class ZakladniReportBuilder
{
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
            .OrderBy(s => s.MinOrder)
            .Select(s => new ReportSection(s.Key, s.Charts))
            .ToList();

        return new ZakladniReportViewModel(projektId, dataset.ProjektNazev, obdobi, sections);
    }
}
