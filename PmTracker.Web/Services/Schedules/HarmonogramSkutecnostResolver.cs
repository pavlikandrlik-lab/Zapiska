namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Jeden kandidátní binding pro auto-fill skutečnosti konkrétního kroku.
/// </summary>
/// <param name="ExterniOdkazId">FK na <c>zaznam_externi_odkazy</c>.</param>
/// <param name="Cislo6">6místné číslo tiketu (HOT_ZAZNAMY.id).</param>
/// <param name="TypZaznamu">NES / PMP / PNF (potřebné pro match predikátu dle matice).</param>
/// <param name="PredikatKey">K3 / K4_K7 / K6 / K10 — viz <see cref="HarmonogramKrokDatumMapping"/>.</param>
/// <param name="Datum">Datum vyjádření = kandidát pro SkutecnostDatum kroku.</param>
public sealed record BindingKandidat(
    int ExterniOdkazId,
    string Cislo6,
    string TypZaznamu,
    string PredikatKey,
    DateTime Datum);

/// <summary>
/// Výsledek resolveru pro jeden krok harmonogramu.
/// </summary>
/// <param name="Datum">Vybrané datum skutečnosti (MAX default nebo preferred), <c>null</c> pokud nejsou kandidáti.</param>
/// <param name="VybranyExterniOdkazId">ID externí vazby, z níž datum pochází.</param>
/// <param name="Kandidati">Všichni kandidáti, seřazení sestupně podle data (MAX first).</param>
/// <param name="PreferredFallbackApplied">
/// True pokud caller předal <c>preferredExterniOdkazId</c>, ale tento binding nebyl mezi kandidáty
/// (např. harvest ho odstranil) → fallback na MAX default. Caller by měl preferred flag clear-nout.
/// </param>
public sealed record ResolvedSkutecnost(
    DateTime? Datum,
    int? VybranyExterniOdkazId,
    IReadOnlyList<BindingKandidat> Kandidati,
    bool PreferredFallbackApplied);

/// <summary>
/// Plán 4 Feature C Task 3 — pure-logic resolver skutečnosti.
///
/// Vstup: (KrokPoradi, seznam všech bindings záznamu, volitelný preferredExterniOdkazId).
/// Výstup: <see cref="ResolvedSkutecnost"/> — vybrané datum + kandidáti.
///
/// Logika (dle decision brief C-Q2):
/// <list type="number">
///   <item>Filtrujeme bindings, jejichž predikát pro <c>krokPoradi</c> + <c>TypZaznamu</c>
///         odpovídá <see cref="HarmonogramKrokDatumMapping"/>.</item>
///   <item>Seřadíme sestupně podle data (MAX first).</item>
///   <item>Pokud je nastaven <c>preferredExterniOdkazId</c> a odpovídající binding je mezi kandidáty,
///         vybereme ho. Jinak (preferred chybí nebo není v kandidátech) vybereme MAX.</item>
///   <item>Pokud preferred není v kandidátech ale kandidáti jsou, signalizujeme
///         <see cref="ResolvedSkutecnost.PreferredFallbackApplied"/> = true, caller clear-uje preferred flag.</item>
/// </list>
/// </summary>
public static class HarmonogramSkutecnostResolver
{
    public static ResolvedSkutecnost Resolve(
        int krokPoradi,
        IReadOnlyList<BindingKandidat> allBindingsForZaznam,
        int? preferredExterniOdkazId)
    {
        var matched = allBindingsForZaznam
            .Where(b => HarmonogramKrokDatumMapping.GetPredikatKey(b.TypZaznamu, krokPoradi) == b.PredikatKey);

        // Spec 2026-04-28 §2: Krok 1 „příprava zadání" agreguje napříč externími vazbami
        // jako MIN (nejdřívější datum založení tiketu). Ostatní kroky 3, 4, 6, 7, 10
        // používají MAX (nejpozdější datum vyjádření) — výchozí stav z C-Q2.
        var kandidati = (krokPoradi == 1
                ? matched.OrderBy(b => b.Datum)
                : matched.OrderByDescending(b => b.Datum))
            .ThenBy(b => b.ExterniOdkazId) // deterministické tie-break při shodném datu
            .ToList();

        if (kandidati.Count == 0)
        {
            return new ResolvedSkutecnost(
                Datum: null,
                VybranyExterniOdkazId: null,
                Kandidati: kandidati,
                PreferredFallbackApplied: false);
        }

        if (preferredExterniOdkazId.HasValue)
        {
            var preferred = kandidati.FirstOrDefault(k => k.ExterniOdkazId == preferredExterniOdkazId.Value);
            if (preferred is not null)
            {
                return new ResolvedSkutecnost(
                    Datum: preferred.Datum,
                    VybranyExterniOdkazId: preferred.ExterniOdkazId,
                    Kandidati: kandidati,
                    PreferredFallbackApplied: false);
            }
            // Preferred není mezi kandidáty → fallback na MAX + signál pro caller (clear preferred).
            var fallback = kandidati[0];
            return new ResolvedSkutecnost(
                Datum: fallback.Datum,
                VybranyExterniOdkazId: fallback.ExterniOdkazId,
                Kandidati: kandidati,
                PreferredFallbackApplied: true);
        }

        var max = kandidati[0];
        return new ResolvedSkutecnost(
            Datum: max.Datum,
            VybranyExterniOdkazId: max.ExterniOdkazId,
            Kandidati: kandidati,
            PreferredFallbackApplied: false);
    }
}
