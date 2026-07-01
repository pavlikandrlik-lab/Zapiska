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
        // Barvy dle Excelu: PMP (kroky 1–5) = MS Office „Zelená, zvýraznění 6" (Accent6 #70AD47),
        // PNF (kroky 6–10) = „Zlatá, zvýraznění 4" (Accent4 #FFC000). Krok 1 je sdílený start → PMP.
        // V každé skupině jemný přechod „světlá 80 %" → „světlá 40 %" (lineární interpolace přes 5 kroků).
        new(1,  "HS01", "1. priprava zadani dodavateli", "#E2EFDA", false, "K1"),
        new(2,  "HS02", "2. konzultace terminu s dodavatelem pred vytvorenim zadani", "#D4E7C7", true,  null),
        new(3,  "HS03", "3. odeslani zadani dodavateli", "#C6E0B4", false, "K3"),
        new(4,  "HS04", "4. dodani navrhu reseni", "#B7D8A1", false, "K4_K7"),
        new(5,  "HS05", "5. vyporadani pripominek", "#A9D08E", true,  null),
        new(6,  "HS06", "6. odeslani pozadavku na vyrobu", "#FFF2CC", false, "K6"),
        new(7,  "HS07", "7. dodani funkcionality dodavatelem", "#FFECB3", false, "K4_K7"),
        new(8,  "HS08", "8. pripominkovani", "#FFE699", true,  null),
        new(9,  "HS09", "9. testovani", "#FFDF80", true,  null),
        new(10, "HS10", "10. nasazeni do provozu", "#FFD966", false, "K10"),
    ];
}
