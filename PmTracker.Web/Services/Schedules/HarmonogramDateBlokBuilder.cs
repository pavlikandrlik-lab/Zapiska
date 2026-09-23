using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

using PmTracker.Web.Services.Records;
namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Pure mapper: z řádků <see cref="ZaznamHarmonogramKrokEntity"/> (datum-model) staví UI ViewModely
/// přes <see cref="ScheduleDateCalculator"/>. Odvozené hodnoty pro zpětně kompatibilní šablonu:
/// OdchylkaDni = (skutečnost − plán) jen u vyplněných (jinak NULL = krok nenastal),
/// TrvaniDni = (plán konec − plán začátek). Bez DB závislosti → unit-testovatelné.
/// </summary>
public static class HarmonogramDateBlokBuilder
{
    public static IReadOnlyList<HarmonogramKrokEditViewModel> BuildKroky(
        DateTime start,
        IReadOnlyCollection<ZaznamHarmonogramKrokEntity> rows,
        DateTime today)
    {
        var byPoradi = rows.GroupBy(r => (int)r.Poradi).ToDictionary(g => g.Key, g => g.First());
        var steps = HarmonogramKroky.Vse
            .Select(def =>
            {
                byPoradi.TryGetValue(def.Poradi, out var row);
                return new ScheduleDateStep(def.Poradi, row?.PlanDatum, row?.SkutecnostDatum);
            })
            .ToList();

        var computed = ScheduleDateCalculator.Compute(start, steps, today);
        var resultByPoradi = computed.ToDictionary(x => x.Poradi);

        return HarmonogramKroky.Vse
            .Select(def =>
            {
                var c = resultByPoradi[def.Poradi];
                byPoradi.TryGetValue(def.Poradi, out var row);
                var trvaniDni = (c.PlanEnd - c.PlanStart).Days;
                // OdchylkaDni + SkutecneDatum jen u SKUTEČNĚ vyplněných (ne u projektovaného aktuálního kroku).
                var skutecneDatum = row?.SkutecnostDatum?.Date;
                int? odchylka = skutecneDatum.HasValue ? (skutecneDatum.Value - c.PlanEnd).Days : null;

                // 2026-09-03 (bug): read-only zobrazení (karta, tabulka na stránce záznamu)
                // psalo „Automat zatím nenašel vhodné vyjádření", i když skutečnost existovala.
                // Builder plnil jen surový SkutecnostZdroj z DB; buňka se ale řídí podle
                // ZdrojSkutecnosti, které zůstávalo None. Mapování je shodné s editorem
                // (ProjectService.RecordEditorComposition) — jediný rozdíl je, že tady
                // nedohledáváme zdrojové vyjádření (to potřebuje jen editor pro chat ikonu).
                var zdrojSkutecnosti = !skutecneDatum.HasValue
                    ? ZdrojSkutecnosti.None
                    : (SkutecnostZdrojEnum)(row?.SkutecnostZdroj ?? 0) switch
                    {
                        SkutecnostZdrojEnum.Automat => ZdrojSkutecnosti.FromVyjadreni,
                        SkutecnostZdrojEnum.Manual => ZdrojSkutecnosti.Manual,
                        SkutecnostZdrojEnum.Historicka => ZdrojSkutecnosti.Manual,
                        _ => ZdrojSkutecnosti.None
                    };

                return new HarmonogramKrokEditViewModel
                {
                    KrokIndex = def.Poradi,
                    Nazev = def.Nazev,
                    BarvaHex = def.BarvaHex,
                    TrvaniDni = trvaniDni,
                    OdchylkaDni = odchylka,
                    BaselineDatum = c.PlanEnd,
                    // Surové uložené plánové datum (null = nevyplněno) — editor podle něj renderuje
                    // prázdné pole; BaselineDatum (dopočtený konec) zůstává pro bar/tooltip.
                    PlanDatum = row?.PlanDatum?.Date,
                    PlanZacatek = c.PlanStart,
                    SkutecnostZacatek = c.MaSkutecnost ? c.SkutecnostStart : null,
                    SkutecnostKonec = c.MaSkutecnost ? c.SkutecnostEnd : null,
                    SkutecneDatum = skutecneDatum,   // null = nevyplněno (žádný PlanEnd fallback)
                    ZdrojSkutecnosti = zdrojSkutecnosti,
                    Stav = c.Stav,
                    IsManualKrok = def.JeManualni,
                    SkutecnostRezim = (SkutecnostRezimEnum)(row?.SkutecnostRezim ?? 0),
                    SkutecnostZdroj = (SkutecnostZdrojEnum)(row?.SkutecnostZdroj ?? 0),
                    PreferredExterniOdkazId = row?.PreferredExterniOdkazId,
                };
            })
            .ToList();
    }

    /// <summary>
    /// Datum-model (Fáze 3b): server-side pozice baru (left%/width% + markery) z krok rows.
    /// Jediný zdroj pravdy pro statická zobrazení — klient (block.js) je nepřepočítává.
    /// </summary>
    public static ScheduleBarLayout BuildBarLayout(
        DateTime start,
        IReadOnlyCollection<ZaznamHarmonogramKrokEntity> rows,
        DateTime termin,
        DateTime today)
    {
        var byPoradi = rows.GroupBy(r => (int)r.Poradi).ToDictionary(g => g.Key, g => g.First());
        var steps = HarmonogramKroky.Vse
            .Select(def =>
            {
                byPoradi.TryGetValue(def.Poradi, out var row);
                return new ScheduleDateStep(def.Poradi, row?.PlanDatum, row?.SkutecnostDatum);
            })
            .ToList();

        var computed = ScheduleDateCalculator.Compute(start, steps, today);
        return ScheduleBarLayoutCalculator.Compute(start.Date, termin, today, computed);
    }

    public static HarmonogramSouhrnViewModel BuildSouhrn(
        DateTime start,
        IReadOnlyCollection<ZaznamHarmonogramKrokEntity> rows,
        DateTime termin,
        DateTime today)
    {
        var byPoradi = rows.GroupBy(r => (int)r.Poradi).ToDictionary(g => g.Key, g => g.First());
        var steps = HarmonogramKroky.Vse
            .Select(def =>
            {
                byPoradi.TryGetValue(def.Poradi, out var row);
                return new ScheduleDateStep(def.Poradi, row?.PlanDatum, row?.SkutecnostDatum);
            })
            .ToList();

        var s = ScheduleDateCalculator.Summarize(start, steps, termin, today);
        var aktualniNazev = s.AktualniKrokPoradi.HasValue
            ? HarmonogramKroky.Vse.FirstOrDefault(k => k.Poradi == s.AktualniKrokPoradi.Value)?.Nazev
            : null;

        return new HarmonogramSouhrnViewModel
        {
            BaselineDokonceni = s.PlanoveDokonceni,
            TerminUkolu = s.Termin,
            CelkoveTrvaniDni = Math.Max(0, (s.PlanoveDokonceni - start.Date).Days),
            AktualniKrokPoradi = s.AktualniKrokPoradi,
            AktualniKrokNazev = aktualniNazev,
            PrekroceniDni = s.PrekroceniDni,
            Dokonceno = s.Dokonceno,
        };
    }
}
