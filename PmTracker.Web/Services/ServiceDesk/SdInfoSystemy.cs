namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Záznam hardkódovaného katalogu Informačních systémů pro projektový dashboard.
/// </summary>
public sealed record SdInfoSystem(int Id, string Zkratka, string Nazev);

/// <summary>
/// Hardkódovaný katalog Informačních systémů v PM Trackeru
/// (dle decision brief C-Q3 + memory <c>project_servicedesk_infosystem_binding</c>).
///
/// 2026-04-24: SD produkce má 3 záznamy v <c>HOT_IS</c> (FIS, ISSP, X_FIS).
/// X_FIS je mimo scope PM Trackeru (rozhodnutí user — Plán 5 Sprint B).
///
/// Hodnoty <see cref="SdInfoSystem.Id"/> odpovídají <c>HOT_IS.ID</c> v intranetNEW.
/// <b>TODO před nasazením Sprint B do produkce ověřit:</b>
/// <code>
/// SELECT ID, zkratka, nazev FROM intranetNEW.dbo.HOT_IS
/// WHERE zkratka IN ('FIS', 'ISSP') ORDER BY ID;
/// </code>
/// Pokud ID v produkci není 1 / 2, uprav <see cref="FisId"/> a <see cref="IsspId"/>.
/// Paragraph intentionally opt-in: hardkódovaný číselník je záměrný (memory — IS je
/// stabilní enum, ne runtime dotaz na SD pro dropdown).
/// </summary>
public static class SdInfoSystemy
{
    /// <summary>Reálné <c>HOT_IS.ID</c> pro Finanční informační systém (FIS).</summary>
    public const int FisId = 1;

    /// <summary>Reálné <c>HOT_IS.ID</c> pro Informační systém služebních poměrů (ISSP).</summary>
    public const int IsspId = 2;

    /// <summary>
    /// Výchozí katalog IS v pořadí, v jakém má být prezentován v UI dropdownu
    /// (řazeno podle ID vzestupně).
    /// </summary>
    public static readonly IReadOnlyList<SdInfoSystem> Vychozi = new[]
    {
        new SdInfoSystem(FisId, "FIS", "Finanční informační systém"),
        new SdInfoSystem(IsspId, "ISSP", "Informační systém služebních poměrů"),
    };

    /// <summary>Vrátí IS s daným <paramref name="id"/> nebo <c>null</c>, pokud není v katalogu.</summary>
    public static SdInfoSystem? ById(int id)
        => Vychozi.FirstOrDefault(x => x.Id == id);

    /// <summary>
    /// Je-li hodnota <c>null</c>, vrací <c>false</c>; jinak ověří, že IS je v katalogu.
    /// Primárně určeno pro validaci <c>ServiceDeskInfoSystemId</c> při ukládání projektu.
    /// </summary>
    public static bool IsSupported(int? id)
        => id is int n && Vychozi.Any(x => x.Id == n);
}
