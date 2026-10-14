using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Datum-model (2026-06-12) — validace polí návrhových payloadů. Kroky identifikovány
/// pořadím (1–10) místo KrokKey. Skutečnost návrhu validuje ScheduleChronologyValidator
/// a zapisuje ScheduleActualApplier (spec 2026-10-14).
/// </summary>
public static class ManualProposalFieldValidator
{
    public static void ValidateHarmonogramVazby(IReadOnlyList<HarmonogramVazbaDto> vazby, int externiVazbyCount)
    {
        if (vazby.Count == 0)
        {
            return;
        }

        var seen = new HashSet<int>();
        foreach (var v in vazby)
        {
            if (v.Poradi is < 1 or > 10)
            {
                throw new RecordValidationException(
                    "Vazba vyjádření na krok musí mít platné pořadí (1–10).",
                    new[] { new RecordValidationIssue("HarmonogramVazby", "Vazba vyjádření na krok musí mít platné pořadí (1–10).", "schedule", "vazba.poradi", v.Poradi.ToString()) },
                    "ValidateHarmonogramVazby: Poradi mimo 1–10");
            }

            if (!seen.Add(v.Poradi))
            {
                throw new RecordValidationException(
                    "Pro jeden krok harmonogramu lze navést jen jednu bublinu.",
                    new[] { new RecordValidationIssue("HarmonogramVazby", "Pro jeden krok harmonogramu lze navést jen jednu bublinu.", "schedule", "vazba.duplicate", v.Poradi.ToString()) },
                    "ValidateHarmonogramVazby: duplicate Poradi");
            }

            if (v.ExterniOdkazIndex < 0 || v.ExterniOdkazIndex >= externiVazbyCount)
            {
                throw new RecordValidationException(
                    "Index externí vazby v návrhu je mimo rozsah.",
                    new[] { new RecordValidationIssue("HarmonogramVazby", "Index externí vazby v návrhu je mimo rozsah.", "schedule", "vazba.invalid-index", v.ExterniOdkazIndex.ToString()) },
                    $"ValidateHarmonogramVazby: invalid ExterniOdkazIndex={v.ExterniOdkazIndex}");
            }

            if (v.HotVyjadreniId <= 0)
            {
                throw new RecordValidationException(
                    "Identifikátor vyjádření musí být kladné číslo.",
                    new[] { new RecordValidationIssue("HarmonogramVazby", "Identifikátor vyjádření musí být kladné číslo.", "schedule", "vazba.invalid-hot-vyjadreni-id", v.HotVyjadreniId.ToString()) },
                    $"ValidateHarmonogramVazby: invalid HotVyjadreniId={v.HotVyjadreniId}");
            }
        }
    }
}
