using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

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

                return new HarmonogramKrokEditViewModel
                {
                    KrokIndex = def.Poradi,
                    Nazev = def.Nazev,
                    BarvaHex = def.BarvaHex,
                    TrvaniDni = trvaniDni,
                    OdchylkaDni = odchylka,
                    BaselineDatum = c.PlanEnd,
                    SkutecneDatum = skutecneDatum,   // null = nevyplněno (žádný PlanEnd fallback)
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
