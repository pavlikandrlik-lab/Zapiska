namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

public enum ChartKind
{
    Bar,
    StackedBar,
    Pie,
    Line,
    StatCards
}

public sealed record ChartSeries(string Label, IReadOnlyList<double> Values, string? ColorToken = null);

public sealed record StatCard(string Label, string Value, string? Note = null);

/// <summary>
/// Rendering-agnostický kontrakt: podklady pro JEDEN graf. Výstup jednoho
/// <see cref="IZakladniChartProvider"/>. Generický renderer mapuje <see cref="Kind"/>
/// na konkrétní SVG/HTML komponentu.
/// </summary>
public sealed record ChartData
{
    public required string Key { get; init; }
    public required ChartKind Kind { get; init; }
    public required string Title { get; init; }

    /// <summary>Hlavní sdělení „na první pohled". Volitelné.</summary>
    public string? Insight { get; init; }

    /// <summary>Metodická poznámka / caveat. Volitelné.</summary>
    public string? Note { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<ChartSeries> Series { get; init; } = [];
    public IReadOnlyList<StatCard> Stats { get; init; } = [];

    public static ChartData ForSeries(
        string key,
        ChartKind kind,
        string title,
        IReadOnlyList<string> categories,
        IReadOnlyList<ChartSeries> series,
        string? insight = null,
        string? note = null)
        => new()
        {
            Key = key,
            Kind = kind,
            Title = title,
            Categories = categories,
            Series = series,
            Insight = insight,
            Note = note
        };

    public static ChartData ForStatCards(
        string key,
        string title,
        IReadOnlyList<StatCard> stats,
        string? insight = null,
        string? note = null)
        => new()
        {
            Key = key,
            Kind = ChartKind.StatCards,
            Title = title,
            Stats = stats,
            Insight = insight,
            Note = note
        };
}
