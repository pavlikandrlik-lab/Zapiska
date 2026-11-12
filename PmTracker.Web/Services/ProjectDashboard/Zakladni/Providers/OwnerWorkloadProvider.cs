using static PmTracker.Web.Services.ProjectDashboard.Zakladni.UkolyMetriky;

namespace PmTracker.Web.Services.ProjectDashboard.Zakladni.Providers;

/// <summary>
/// Sekce „Lidé a spolupráce": vytížení vlastníků — otevřené úkoly v termínu / po termínu, top 10 + „Ostatní"
/// (spec 2026-10-15 §11.2, vizuál 13). Lidé jedním odstínem podle pořadí, nikdy duhou.
/// </summary>
public sealed class OwnerWorkloadProvider : IZakladniChartProvider
{
    private const int Top = 10;

    public string Key => "owner-workload";
    public string SectionKey => "lide";
    public int Order => 10;

    public ChartData Build(ZakladniDataset ds)
    {
        var stavy = StavyPodleId(ds);
        var jmena = ds.Vlastnici.ToDictionary(o => o.Id, o => o.Jmeno);

        var podleVlastnika = ds.Ukoly
            .Where(r => JeOtevreny(r, stavy))
            .GroupBy(r => r.VlastnikId)
            .Select(g => new
            {
                Jmeno = jmena.TryGetValue(g.Key, out var j) && !string.IsNullOrWhiteSpace(j) ? j : $"Osoba {g.Key}",
                VTerminu = g.Count(r => !JePoTerminu(r, ds.Dnes)),
                PoTerminu = g.Count(r => JePoTerminu(r, ds.Dnes)),
            })
            .OrderByDescending(x => x.VTerminu + x.PoTerminu)
            .ThenByDescending(x => x.PoTerminu)
            .ThenBy(x => x.Jmeno, StringComparer.Create(Cs, ignoreCase: true))
            .ToList();

        var radky = podleVlastnika.Take(Top).Select(x => (x.Jmeno, x.VTerminu, x.PoTerminu)).ToList();
        var zbytek = podleVlastnika.Skip(Top).ToList();
        if (zbytek.Count > 0)
        {
            radky.Add(("Ostatní", zbytek.Sum(x => x.VTerminu), zbytek.Sum(x => x.PoTerminu)));
        }

        var nejvicPoTerminu = podleVlastnika.OrderByDescending(x => x.PoTerminu).FirstOrDefault();
        return ChartData.ForSeries(Key, ChartKind.StackedHorizontalBar, "Vytížení vlastníků",
            radky.Select(r => r.Item1).ToList(),
            [
                new ChartSeries("V termínu", radky.Select(r => (double)r.Item2).ToList(), "--app-chart-1"),
                new ChartSeries("Po termínu", radky.Select(r => (double)r.Item3).ToList(), "--background-status-error"),
            ],
            insight: nejvicPoTerminu is { PoTerminu: > 0 }
                ? $"Nejvíc úkolů po termínu má {nejvicPoTerminu.Jmeno} ({nejvicPoTerminu.PoTerminu.ToString(Cs)})."
                : podleVlastnika.Count > 0 ? "Žádný vlastník nemá otevřený úkol po termínu." : null,
            note: zbytek.Count > 0 ? $"„Ostatní“ = dalších {zbytek.Count.ToString(Cs)} vlastníků." : null) with
        {
            Span = ChartSpan.Full,
            EmptyMessage = "Žádný úkol není otevřený.",
        };
    }
}
