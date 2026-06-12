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
        IReadOnlyCollection<ZaznamHarmonogramKrokEntity> rows)
    {
        var byPoradi = rows.GroupBy(r => (int)r.Poradi).ToDictionary(g => g.Key, g => g.First());
        var steps = HarmonogramKroky.Vse
            .Select(def =>
            {
                byPoradi.TryGetValue(def.Poradi, out var row);
                return new ScheduleDateStep(def.Poradi, row?.PlanDatum, row?.SkutecnostDatum);
            })
            .ToList();

        var computed = ScheduleDateCalculator.Compute(start, steps);
        var resultByPoradi = computed.ToDictionary(x => x.Poradi);

        return HarmonogramKroky.Vse
            .Select(def =>
            {
                var c = resultByPoradi[def.Poradi];
                byPoradi.TryGetValue(def.Poradi, out var row);
                var trvaniDni = (c.PlanEnd - c.PlanStart).Days;
                int? odchylka = c.MaSkutecnost ? (c.SkutecnostEnd - c.PlanEnd).Days : null;

                return new HarmonogramKrokEditViewModel
                {
                    KrokIndex = def.Poradi,
                    Nazev = def.Nazev,
                    BarvaHex = def.BarvaHex,
                    TrvaniDni = trvaniDni,
                    OdchylkaDni = odchylka,
                    BaselineDatum = c.PlanEnd,
                    SkutecneDatum = c.MaSkutecnost ? c.SkutecnostEnd : c.PlanEnd,
                    IsManualKrok = def.JeManualni,
                    SkutecnostRezim = (SkutecnostRezimEnum)(row?.SkutecnostRezim ?? 0),
                    SkutecnostZdroj = (SkutecnostZdrojEnum)(row?.SkutecnostZdroj ?? 0),
                    PreferredExterniOdkazId = row?.PreferredExterniOdkazId,
                };
            })
            .ToList();
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

        return new HarmonogramSouhrnViewModel
        {
            BaselineDokonceni = s.PlanoveDokonceni,
            SkutecneDokonceni = s.SkutecneDokonceni,
            TerminUkolu = s.Termin,
            CelkoveTrvaniDni = Math.Max(0, (s.PlanoveDokonceni - start.Date).Days),
            CelkovaOdchylkaDni = (s.SkutecneDokonceni - s.PlanoveDokonceni).Days,
            Stihame = s.Stihame,
            PrekroceniDni = s.PrekroceniDni,
        };
    }
}
