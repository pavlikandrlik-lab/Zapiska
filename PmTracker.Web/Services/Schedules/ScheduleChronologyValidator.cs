using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Chronologie datumů harmonogramu (datum-model). Jediný zdroj pravidel pro přímé uložení
/// záznamu (RecordService.SaveRecord) i pro odeslání návrhu (RecordProposalService.SubmitCommands),
/// aby návrh nemohl obejít validaci, kterou běžné uložení vynucuje.
///
/// Ruční vstupy musí být neklesající (stejný den OK):
///   - Plán: pro každý krok N platí plán(N) ≥ plán(předchozího kroku s plánem).
///   - Skutečnost (spec 2026-10-14 R7): neklesající mezi hodnotami, které po uložení patří
///     uživateli (2/5/8/9 vždy, 1/3/4/6/7/10 v ručním režimu), jen u dvojic se změnou.
/// Auto skutečnost (harvest) se NEvaliduje — pořadí zajišťuje vytěžovací algoritmus.
///
/// Prázdný vstup (žádný plán / žádná skutečnost) = žádná issue. Návrh založení záznamu
/// tak neřeší skutečnost, protože ji ve formuláři nelze vyplnit (HideActual).
/// </summary>
public static class ScheduleChronologyValidator
{
    /// <summary>Připojí případné chronologické issue do sdílené kolekce (agregační režim — SaveRecord).</summary>
    public static void CollectIssues(SaveRecordCommand command, ICollection<RecordValidationIssue> issues)
    {
        // Plán: field key musí ukazovat na původní index v posted poli (ne na seřazené pořadí).
        DateTime? prevPlan = null;
        int prevPlanPoradi = 0;
        foreach (var pair in command.HarmonogramHodnoty
                     .Select((item, index) => (item, index))
                     .Where(x => x.item.Poradi is >= 1 and <= 10 && x.item.PlanDatum.HasValue)
                     .OrderBy(x => x.item.Poradi))
        {
            var planDate = pair.item.PlanDatum!.Value.Date;
            if (prevPlan.HasValue && planDate < prevPlan.Value)
            {
                issues.Add(new RecordValidationIssue(
                    $"HarmonogramHodnoty[{pair.index}].PlanDatum",
                    $"Plán kroku {pair.item.Poradi} nesmí být dříve než plán kroku {prevPlanPoradi}.",
                    "schedule",
                    "schedule_plan_chronology",
                    planDate.ToString("yyyy-MM-dd")));
            }
            prevPlan = planDate;
            prevPlanPoradi = pair.item.Poradi;
        }

        // Spec 2026-10-14 R7: skutečnost se kontroluje jen mezi hodnotami, které po uložení patří
        // uživateli (kroky 2/5/8/9 vždy, 1/3/4/6/7/10 jen v ručním režimu), a jen u dvojic, kde se
        // aspoň jedna hodnota v tomto uložení změnila. Stará data (třeba převzatá od automatu),
        // na která uživatel nesáhl, uložení neblokují.
        var rucniRezim = HarmonogramAutoFillRule.Normalize(command.HarmonogramRezim) == HarmonogramAutoFillRule.Manual;
        var uzivatelske = command.HarmonogramHodnoty
            .Select((item, index) => (item, index))
            .Where(x => x.item.Poradi is >= 1 and <= 10 && x.item.SkutecnostDatum.HasValue)
            .Where(x => HarmonogramManualSteps.IsManual(x.item.Poradi) || rucniRezim)
            .OrderBy(x => x.item.Poradi)
            .ToList();
        for (var k = 1; k < uzivatelske.Count; k++)
        {
            var prev = uzivatelske[k - 1];
            var cur = uzivatelske[k];
            var curDate = cur.item.SkutecnostDatum!.Value.Date;
            var dotcena = ScheduleActualApplier.JeZmena(cur.item.SkutecnostDatum, cur.item.SkutecnostDatumPuvodni)
                || ScheduleActualApplier.JeZmena(prev.item.SkutecnostDatum, prev.item.SkutecnostDatumPuvodni);
            if (dotcena && curDate < prev.item.SkutecnostDatum!.Value.Date)
            {
                issues.Add(new RecordValidationIssue(
                    $"HarmonogramHodnoty[{cur.index}].SkutecnostDatum",
                    $"Skutečnost kroku {cur.item.Poradi} nesmí být dříve než skutečnost kroku {prev.item.Poradi}.",
                    "schedule",
                    "schedule_actual_chronology",
                    curDate.ToString("yyyy-MM-dd")));
            }
        }
    }

    /// <summary>Vyhodí <see cref="RecordValidationException"/> při první chronologické chybě (fail-fast režim — návrhy).</summary>
    public static void EnsureChronological(SaveRecordCommand command)
    {
        var issues = new List<RecordValidationIssue>();
        CollectIssues(command, issues);
        if (issues.Count > 0)
        {
            throw new RecordValidationException(
                issues[0].Message,
                issues,
                $"ScheduleChronologyValidator: {issues.Count} chronologická chyba/y v harmonogramu návrhu.");
        }
    }
}
