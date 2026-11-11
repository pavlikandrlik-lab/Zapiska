namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

public sealed record ReportSection(string Key, IReadOnlyList<ChartData> Charts);

public sealed record ZakladniReportViewModel(
    int ProjektId,
    string ProjektNazev,
    Obdobi Obdobi,
    IReadOnlyList<ReportSection> Sections);

/// <summary>Nadpis a jednovětý popis sekce Statistik (spec 2026-10-15 §11.1, plán Task 14).</summary>
public static class ZakladniSekce
{
    public static (string Nadpis, string Popis) Popis(string key) => key switch
    {
        "prehled" => ("Přehled", "Hlavní čísla za období a srovnání s předchozím obdobím."),
        "prubeh" => ("Průběh práce", "Stíháme dokončovat, co přibývá?"),
        "terminy" => ("Termíny", "Plní se plán a kde se termíny posouvají."),
        "lide" => ("Lidé a spolupráce", "Vytížení vlastníků, účast na jednáních a komunikace."),
        "servicedesk" => ("ServiceDesk a dodavatelé", "Tickety, prodlení dodavatelů a objednávky."),
        _ => (key, string.Empty),
    };
}
