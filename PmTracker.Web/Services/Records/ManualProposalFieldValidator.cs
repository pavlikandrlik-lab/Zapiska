using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Datum-model (2026-06-12) — validace polí návrhových payloadů. Kroky identifikovány
/// pořadím (1–10) místo KrokKey.
/// </summary>
public static class ManualProposalFieldValidator
{
    public static void ValidateManualActualKroky(IReadOnlyList<ManualActualKrokDto> manualKroky, DateOnly today)
    {
        if (manualKroky.Count == 0)
        {
            return;
        }

        var seen = new HashSet<int>();
        foreach (var krok in manualKroky)
        {
            if (krok.Poradi is < 1 or > 10)
            {
                throw new RecordValidationException(
                    "Ruční skutečnost kroku musí mít platné pořadí (1–10).",
                    new[] { new RecordValidationIssue("ManualActualKroky", "Ruční skutečnost kroku musí mít platné pořadí (1–10).", "schedule", "manual.poradi", krok.Poradi.ToString()) },
                    "ValidateManualActualKroky: Poradi mimo 1–10");
            }

            if (!seen.Add(krok.Poradi))
            {
                throw new RecordValidationException(
                    "Pro jeden krok harmonogramu lze zadat jen jedno ruční datum.",
                    new[] { new RecordValidationIssue("ManualActualKroky", "Pro jeden krok harmonogramu lze zadat jen jedno ruční datum.", "schedule", "manual.duplicate", krok.Poradi.ToString()) },
                    "ValidateManualActualKroky: duplicate Poradi");
            }
        }
    }

    /// <summary>Ověří, že ruční skutečnost míří jen na manuální kroky 2/5/8/9.</summary>
    public static void ValidateManualKrokKeysAreManualOnly(IReadOnlyList<ManualActualKrokDto> manualKroky)
    {
        foreach (var mk in manualKroky)
        {
            if (mk.Poradi is < 1 or > 10)
            {
                throw new RecordValidationException(
                    "Krok harmonogramu pro zadané pořadí neexistuje.",
                    new[] { new RecordValidationIssue("ManualActualKroky", "Neznámý krok harmonogramu.", "schedule", "manual.unknown-krok", mk.Poradi.ToString()) },
                    $"ValidateManualKrokKeysAreManualOnly: Poradi={mk.Poradi} mimo rozsah");
            }

            if (!HarmonogramManualSteps.IsManual(mk.Poradi))
            {
                throw new RecordValidationException(
                    $"Krok {mk.Poradi} je v Auto rezimu — ručně vyplnit lze jen kroky 2, 5, 8 a 9.",
                    new[] { new RecordValidationIssue("ManualActualKroky", $"Krok {mk.Poradi} není manuální (jen 2/5/8/9 jsou manuální).", "schedule", "manual.auto-step", mk.Poradi.ToString()) },
                    $"ValidateManualKrokKeysAreManualOnly: Poradi={mk.Poradi} není manuální");
            }
        }
    }

    /// <summary>
    /// Odmítne payload obsahující skutečnost pro auto-fillované kroky (1/3/4/6/7/10).
    /// </summary>
    /// <param name="actualHodnoty">payload.ActualHarmonogramHodnoty navrhovaného harmonogramu.</param>
    /// <param name="autoFilledPoradi">Set pořadí auto-fillovaných kroků.</param>
    public static void ValidateAutoStepNotInProposal(
        IReadOnlyList<SaveRecordHarmonogramValueCommand> actualHodnoty,
        IReadOnlySet<int> autoFilledPoradi)
    {
        if (actualHodnoty.Count == 0 || autoFilledPoradi.Count == 0)
        {
            return;
        }

        foreach (var item in actualHodnoty)
        {
            if (item.SkutecnostDatum.HasValue && autoFilledPoradi.Contains(item.Poradi))
            {
                var msg = $"Krok {item.Poradi} je v Auto rezimu — návrh úpravy skutečnosti není možný. " +
                          "Použij přímou editaci s ToggleRezim=Manual, nebo edituj jen manuální kroky 2/5/8/9.";
                throw new RecordValidationException(
                    msg,
                    new[] { new RecordValidationIssue("HarmonogramHodnoty", msg, "schedule", "schedule.auto-step-rejected", item.Poradi.ToString()) },
                    $"ValidateAutoStepNotInProposal: auto step Poradi={item.Poradi} v payloadu");
            }
        }
    }

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
