namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Definice jednoho z 10 pevných kroků harmonogramu. Nahrazuje dřívější DB číselník
/// (ciselnik_harmonogram_typu) + verzování šablon — kroky jsou napevno v kódu.
/// </summary>
/// <param name="Poradi">1–10, pořadí kroku.</param>
/// <param name="Kod">Stabilní kód kroku (HS01–HS10).</param>
/// <param name="Nazev">Zobrazovaný název.</param>
/// <param name="BarvaHex">Barva segmentu (#RRGGBB).</param>
/// <param name="JeManualni">true pro kroky 2,5,8,9 (skutečnost se zadává ručně, nevytěžuje se).</param>
/// <param name="HarvestPredikat">Predikát vytěžení (K1/K3/K4_K7/K6/K10) nebo null u manuálních kroků.</param>
public sealed record HarmonogramKrokDefinice(
    int Poradi,
    string Kod,
    string Nazev,
    string BarvaHex,
    bool JeManualni,
    string? HarvestPredikat);

/// <summary>10 pevných kroků harmonogramu — jediný zdroj pravdy (žádná DB šablona/verze).</summary>
public static class HarmonogramKroky
{
    public static readonly IReadOnlyList<HarmonogramKrokDefinice> Vse =
    [
        new(1,  "HS01", "1. priprava zadani dodavateli", "#EF4444", false, "K1"),
        new(2,  "HS02", "2. konzultace terminu s dodavatelem pred vytvorenim zadani", "#F97316", true,  null),
        new(3,  "HS03", "3. odeslani zadani dodavateli", "#F59E0B", false, "K3"),
        new(4,  "HS04", "4. dodani navrhu reseni", "#84CC16", false, "K4_K7"),
        new(5,  "HS05", "5. vyporadani pripominek", "#22C55E", true,  null),
        new(6,  "HS06", "6. odeslani pozadavku na vyrobu", "#14B8A6", false, "K6"),
        new(7,  "HS07", "7. dodani funkcionality dodavatelem", "#06B6D4", false, "K4_K7"),
        new(8,  "HS08", "8. pripominkovani", "#3B82F6", true,  null),
        new(9,  "HS09", "9. testovani", "#6366F1", true,  null),
        new(10, "HS10", "10. nasazeni do provozu", "#8B5CF6", false, "K10"),
    ];
}
