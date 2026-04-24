namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Plán 4 Feature C Task 2 — matice (TypZaznamu × KrokPoradi) → PredikatKey
/// pro auto-fill skutečnosti harmonogramu z vyjádření ServiceDesku.
///
/// Zdroj pravdy: docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §3/3.1.
///
/// Automaticky plněné kroky per typ tiketu (ruční kroky 2/5/8/9 a datum_zalozeni
/// krok 1 matici nevyužívají, proto vrací <c>null</c>):
/// <list type="bullet">
///   <item>NES — žádné automatické vyjádření (jediný krok je K1 = datum_zalozeni)</item>
///   <item>PMP — K3 (odeslání dodavateli), K4 (dodání řešení)</item>
///   <item>PNF — K6 (odeslání požadavku / kalkulace akceptována), K7 (dodání funkcionality),
///              K10 (nasazení / archivace)</item>
/// </list>
///
/// Vrácený <c>PredikatKey</c> odpovídá pojmenování v <see cref="ServiceDesk.HarvestPredicateKind"/>:
/// <list type="bullet">
///   <item><c>"K3"</c> = <c>K3_OdeslaniZadaniPmp</c></item>
///   <item><c>"K4_K7"</c> = <c>K4_K7_DodaniReseni</c> (PMP→4, PNF→7 řeší harvester)</item>
///   <item><c>"K6"</c> = <c>K6_OdeslaniPozadavku</c></item>
///   <item><c>"K10"</c> = <c>K10_NasazeniArchivace</c></item>
/// </list>
/// </summary>
public static class HarmonogramKrokDatumMapping
{
    /// <summary>PredikatKey <c>"K3"</c> — odeslání zadání dodavateli (PMP).</summary>
    public const string PredikatK3 = "K3";
    /// <summary>PredikatKey <c>"K4_K7"</c> — dodání řešení (PMP K4 / PNF K7).</summary>
    public const string PredikatK4K7 = "K4_K7";
    /// <summary>PredikatKey <c>"K6"</c> — odeslání požadavku / kalkulace akceptována (PNF).</summary>
    public const string PredikatK6 = "K6";
    /// <summary>PredikatKey <c>"K10"</c> — nasazení do provozu / archivace (PNF).</summary>
    public const string PredikatK10 = "K10";

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> Matrix =
        new Dictionary<string, IReadOnlyDictionary<int, string>>(StringComparer.Ordinal)
        {
            ["NES"] = new Dictionary<int, string>(),
            ["PMP"] = new Dictionary<int, string>
            {
                [3] = PredikatK3,
                [4] = PredikatK4K7,
            },
            ["PNF"] = new Dictionary<int, string>
            {
                [6] = PredikatK6,
                [7] = PredikatK4K7,
                [10] = PredikatK10,
            },
        };

    /// <summary>
    /// Vrátí PredikatKey pro daný typ ticketu + krok v harmonogramu.
    /// Vrací <c>null</c> pokud krok nemá auto-fill mapování (např. NES, ruční kroky 2/5/8/9,
    /// krok mimo aktivní sadu typu, neznámý typ).
    /// </summary>
    public static string? GetPredikatKey(string typZaznamu, int krokPoradi)
    {
        if (string.IsNullOrWhiteSpace(typZaznamu))
        {
            return null;
        }
        var normalized = typZaznamu.Trim().ToUpperInvariant();
        if (!Matrix.TryGetValue(normalized, out var map))
        {
            return null;
        }
        return map.TryGetValue(krokPoradi, out var key) ? key : null;
    }

    /// <summary>
    /// True pokud daný krok v daném typu tiketu má automatické mapování na vyjádření.
    /// </summary>
    public static bool IsAutomatickyKrok(string typZaznamu, int krokPoradi)
        => GetPredikatKey(typZaznamu, krokPoradi) is not null;

    /// <summary>Seznam podporovaných typů tiketu.</summary>
    public static IReadOnlyCollection<string> SupportedTypes { get; } =
        Matrix.Keys.ToArray();
}
