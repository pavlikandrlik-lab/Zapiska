namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Typy textových predikátů, které dokážeme z HOT_VYJADRENI.popis detekovat.
/// Spec §8.1 — ServiceDesk nemá strukturovaná data, spoléháme na striktní
/// text match. Stejný text může vygenerovat různé kroky dle typu tiketu (PMP vs PNF),
/// proto downstream mapping na KrokKey vyžaduje znalost typu tiketu.
/// </summary>
public enum HarvestPredicateKind
{
    None = 0,

    /// <summary>K3 (PMP) — „Záznam byl založen a předán dodavateli k řešení pod značkou: …".</summary>
    K3_OdeslaniZadaniPmp,

    /// <summary>K4 (PMP) a K7 (PNF) — „Dodavatel přidal řešení". Poslední výskyt (DESC).</summary>
    K4_K7_DodaniReseni,

    /// <summary>K6 (PNF) — „Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.".</summary>
    K6_OdeslaniPozadavku,

    /// <summary>K10 (PMP+PNF) — „Záznam byl převeden do archivu.".</summary>
    K10_NasazeniArchivace,

    /// <summary>Plán dodání — „… předal záznam dodavateli : … s termínem plnění dodavatele …".</summary>
    PlanDodani,

    /// <summary>NES Datum objednání — „Záznam byl předán dodavateli k řešení." (kratší než K6, jen pro NES tickety).</summary>
    NES_DatumObjednani,

    /// <summary>NES Datum dodání — „Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo …" (jen pro NES tickety).</summary>
    NES_DatumDodani
}

/// <summary>
/// Textové predikáty pro klasifikaci HOT_VYJADRENI.popis podle spec §8.1.
/// Case-insensitive, diakritika respektována (default SQL Server Czech_CI_AS).
/// </summary>
public static class HarvestPredicates
{
    private const string PhraseK3 = "Záznam byl založen a předán dodavateli k řešení pod značkou:";
    private const string PhraseK4K7 = "Dodavatel přidal řešení";
    private const string PhraseK6 = "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.";
    private const string PhraseK10 = "Záznam byl převeden do archivu.";
    private const string PhrasePlanPartA = "předal záznam dodavateli :";
    private const string PhrasePlanPartB = "s termínem plnění dodavatele";
    private const string PhraseNesObjednani = "Záznam byl předán dodavateli k řešení.";
    private const string PhraseNesDodani = "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo";

    /// <summary>
    /// Klasifikace jednoho vyjádření. Pořadí rozhoduje (specifičtější predikáty první),
    /// protože např. PlanDodani obsahuje „předal záznam dodavateli" fragment také.
    /// </summary>
    public static HarvestPredicateKind ClassifyPopis(string? popis)
    {
        if (string.IsNullOrWhiteSpace(popis)) return HarvestPredicateKind.None;

        if (Contains(popis, PhraseK10)) return HarvestPredicateKind.K10_NasazeniArchivace;
        if (Contains(popis, PhraseK6)) return HarvestPredicateKind.K6_OdeslaniPozadavku;
        if (Contains(popis, PhraseK3)) return HarvestPredicateKind.K3_OdeslaniZadaniPmp;
        if (Contains(popis, PhrasePlanPartA) && Contains(popis, PhrasePlanPartB))
            return HarvestPredicateKind.PlanDodani;
        if (Contains(popis, PhraseK4K7)) return HarvestPredicateKind.K4_K7_DodaniReseni;

        return HarvestPredicateKind.None;
    }

    private static bool Contains(string text, string phrase)
        => text.Contains(phrase, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// NES-specific klasifikace HOT_VYJADRENI.popis. Vrací jeden z
    /// NES_DatumObjednani / NES_DatumDodani / K10 / None. K10 fráze se sdílí s PMP/PNF
    /// (archivace), zbývající dvě jsou výhradně NES.
    /// PlanDodani pro NES jde z HOT_ZAZNAMY.sla_deadline (DB sloupec), ne z popisu —
    /// proto tady predikát PlanDodani záměrně nedetekujeme.
    /// </summary>
    public static HarvestPredicateKind ClassifyPopisForNes(string? popis)
    {
        if (string.IsNullOrWhiteSpace(popis)) return HarvestPredicateKind.None;

        if (Contains(popis, PhraseK10)) return HarvestPredicateKind.K10_NasazeniArchivace;
        if (Contains(popis, PhraseNesDodani)) return HarvestPredicateKind.NES_DatumDodani;
        if (Contains(popis, PhraseNesObjednani)) return HarvestPredicateKind.NES_DatumObjednani;

        return HarvestPredicateKind.None;
    }

    /// <summary>
    /// SQL LIKE pattern pro konkrétní kind — použitelné v <c>WHERE popis LIKE @pattern</c>
    /// pro přímý DB-side filter místo memory scanu.
    /// </summary>
    public static string GetSqlLikePattern(HarvestPredicateKind kind) => kind switch
    {
        HarvestPredicateKind.K3_OdeslaniZadaniPmp => $"%{PhraseK3}%",
        HarvestPredicateKind.K4_K7_DodaniReseni => $"%{PhraseK4K7}%",
        HarvestPredicateKind.K6_OdeslaniPozadavku => $"%{PhraseK6}%",
        HarvestPredicateKind.K10_NasazeniArchivace => $"%{PhraseK10}%",
        HarvestPredicateKind.PlanDodani => $"%{PhrasePlanPartA}%{PhrasePlanPartB}%",
        HarvestPredicateKind.NES_DatumObjednani => $"%{PhraseNesObjednani}%",
        HarvestPredicateKind.NES_DatumDodani => $"%{PhraseNesDodani}%",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
