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
///   - Ruční skutečnost (2/5/8/9): neklesající mezi zadanými ručními datumy.
/// Auto skutečnost (harvest) se NEvaliduje — pořadí zajišťuje vytěžovací algoritmus.
///
/// Prázdný vstup (žádný plán / žádná ruční skutečnost) = žádná issue. Návrh založení záznamu
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

        // Ruční skutečnost (kroky 2/5/8/9): neklesající mezi zadanými ručními datumy.
        DateTime? prevManual = null;
        int prevManualPoradi = 0;
        foreach (var pair in command.ManualActualKroky
                     .Select((item, index) => (item, index))
                     .Where(x => x.item.Poradi is >= 1 and <= 10 && x.item.AbsolutniDatum.HasValue)
                     .OrderBy(x => x.item.Poradi))
        {
            var skutDate = pair.item.AbsolutniDatum!.Value.ToDateTime(TimeOnly.MinValue).Date;
            if (prevManual.HasValue && skutDate < prevManual.Value)
            {
                issues.Add(new RecordValidationIssue(
                    $"ManualActualKroky[{pair.index}].AbsolutniDatum",
                    $"Ruční skutečnost kroku {pair.item.Poradi} nesmí být dříve než skutečnost kroku {prevManualPoradi}.",
                    "schedule",
                    "schedule_actual_chronology",
                    skutDate.ToString("yyyy-MM-dd")));
            }
            prevManual = skutDate;
            prevManualPoradi = pair.item.Poradi;
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
