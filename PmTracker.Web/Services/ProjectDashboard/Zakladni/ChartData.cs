namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>Druh vizuálu. Grafové druhy kreslí ECharts (<c>echartsOption.js</c>), karty server.</summary>
public enum ChartKind
{
    /// <summary>Sloupce; víc sérií = seskupené vedle sebe.</summary>
    Bar,
    StackedBar,
    /// <summary>Vodorovné sloupce — pro dlouhé popisky a žebříčky (seřazené sestupně).</summary>
    HorizontalBar,
    StackedHorizontalBar,
    Pie,
    /// <summary>Prstenec se součtem uprostřed.</summary>
    Donut,
    Line,
    /// <summary>Malé karty uvnitř karty grafu.</summary>
    StatCards,
    /// <summary>Řada KPI dlaždic (hodnota, trend, stav, ukazatel plnění).</summary>
    Kpi
}

/// <summary>Šířka karty v mřížce reportu: polovina (dvě vedle sebe) nebo celá.</summary>
public enum ChartSpan { Half, Full }

/// <summary>Co je u metriky dobře — růst (dokončeno) nebo pokles (posun termínu).</summary>
public enum TrendPolarity { HigherIsBetter, LowerIsBetter }

/// <summary>Stav KPI; v UI ho vždy nese ikona a text, nikdy jen barva.</summary>
public enum CardStatus { Neutral, Good, Warning, Critical }

/// <summary>Změna proti předchozímu období.</summary>
/// <param name="Delta">Rozdíl (znaménko určuje šipku).</param>
/// <param name="Text">Hotový popis, např. „o 12 % víc než v předchozím období".</param>
/// <param name="GoodWhen">Polarita metriky.</param>
public sealed record CardTrend(double Delta, string Text, TrendPolarity GoodWhen)
{
    public bool JeLepsi => GoodWhen == TrendPolarity.HigherIsBetter ? Delta > 0 : Delta < 0;
    public bool JeHorsi => GoodWhen == TrendPolarity.HigherIsBetter ? Delta < 0 : Delta > 0;
}

/// <param name="ColorToken">Název CSS tokenu barvy série (např. <c>--background-status-error</c>); null = barva podle pořadí.</param>
public sealed record ChartSeries(string Label, IReadOnlyList<double> Values, string? ColorToken = null);

/// <param name="Progress">Ukazatel plnění 0–1 (např. čerpání rozpočtu); null = bez ukazatele.</param>
public sealed record StatCard(string Label, string Value, string? Note = null, CardTrend? Trend = null,
    CardStatus Status = CardStatus.Neutral, double? Progress = null);

/// <summary>
/// Rendering-agnostický kontrakt: podklady pro JEDEN vizuál. Výstup jednoho
/// <see cref="IZakladniChartProvider"/>. Generický renderer mapuje <see cref="Kind"/>
/// na konkrétní komponentu (ECharts / HTML karty).
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

    /// <summary>Barvy výsečí koláče/prstence podle kategorie (názvy CSS tokenů, null = barva podle pořadí).</summary>
    public IReadOnlyList<string?> CategoryColorTokens { get; init; } = [];

    public ChartSpan Span { get; init; } = ChartSpan.Half;

    /// <summary>Jednotka hodnot pro tooltip, popisky a tabulku („%", „dní", „Kč").</summary>
    public string? Unit { get; init; }

    /// <summary>Text prázdného stavu; null = obecná věta.</summary>
    public string? EmptyMessage { get; init; }

    /// <summary>Odkaz k prázdnému stavu (např. nastavení IS); null = bez odkazu.</summary>
    public string? ActionUrl { get; init; }

    public string? ActionText { get; init; }

    /// <summary>
    /// Provider ví, že data existují, i když jsou všechny hodnoty nulové (0 % účast, 0 % včas).
    /// null = rozhodnout podle hodnot.
    /// </summary>
    public bool? MaData { get; init; }

    /// <summary>Není co kreslit: karty bez položek, graf bez nenulové hodnoty (pokud provider neřekl jinak).</summary>
    public bool IsEmpty => MaData is bool maData
        ? !maData
        : Kind is ChartKind.StatCards or ChartKind.Kpi
            ? Stats.Count == 0
            : Series.All(s => s.Values.All(v => v == 0));

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

    /// <summary>Řada KPI dlaždic přes celou šířku.</summary>
    public static ChartData ForKpi(string key, string title, IReadOnlyList<StatCard> stats)
        => new()
        {
            Key = key,
            Kind = ChartKind.Kpi,
            Title = title,
            Stats = stats,
            Span = ChartSpan.Full
        };
}
