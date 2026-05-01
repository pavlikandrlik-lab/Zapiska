using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Plán D — čisté validace polí přidaných do návrhových payloadů:
/// <see cref="ManualActualKrokDto"/> (schémata 2 a 3) a
/// <see cref="HarmonogramVazbaDto"/> (schéma 3). Oddělená třída umožňuje
/// unit-testovat validační logiku bez DB kontextu.
/// </summary>
public static class ManualProposalFieldValidator
{
    /// <summary>
    /// Ruční skutečnost kroku 2/5/8/9:
    /// - <c>KrokKey</c> nesmí být <see cref="Guid.Empty"/>.
    /// - Žádné duplikáty (jeden krok = max jedno datum).
    /// - Datum nesmí být v budoucnosti (vůči <paramref name="today"/>).
    /// <para>
    /// Kontrola, že <c>KrokKey</c> skutečně patří do ručních kroků 2/5/8/9
    /// se dělá až na úrovni approve commandu, protože tam máme k dispozici
    /// schéma harmonogramu konkrétního záznamu.
    /// </para>
    /// </summary>
    public static void ValidateManualActualKroky(IReadOnlyList<ManualActualKrokDto> manualKroky, DateOnly today)
    {
        if (manualKroky.Count == 0)
        {
            return;
        }

        // FIX 2026-05-01 (#7): RecordValidationException místo InvalidOperationException —
        // user vidí UNEXPECTED_SERVER_ERROR pro IOE, RVE má dedicated handling
        // (controller AjaxResultMiddleware ho mapuje na 400 s message).
        var seen = new HashSet<Guid>();
        foreach (var krok in manualKroky)
        {
            if (krok.KrokKey == Guid.Empty)
            {
                throw new RecordValidationException(
                    "Ruční skutečnost kroku musí mít vyplněný identifikátor kroku.",
                    new[] { new RecordValidationIssue("ManualActualKroky", "Ruční skutečnost kroku musí mít vyplněný identifikátor kroku.", "schedule", "manual.krokkey", null) },
                    "ManualProposalFieldValidator.ValidateManualActualKroky: KrokKey == Empty");
            }

            if (!seen.Add(krok.KrokKey))
            {
                throw new RecordValidationException(
                    "Pro jeden krok harmonogramu lze zadat jen jedno ruční datum.",
                    new[] { new RecordValidationIssue("ManualActualKroky", "Pro jeden krok harmonogramu lze zadat jen jedno ruční datum.", "schedule", "manual.duplicate", krok.KrokKey.ToString()) },
                    "ManualProposalFieldValidator.ValidateManualActualKroky: duplicate KrokKey");
            }

            // FIX 2026-05-02: AbsolutniDatum je nullable. NULL = "krok nenastal" → validace
            // se neaplikuje (caller skipne celý krok). Future-date check jen pro vyplněné kroky.
            if (krok.AbsolutniDatum.HasValue && krok.AbsolutniDatum.Value > today)
            {
                throw new RecordValidationException(
                    "Datum skutečnosti kroku nesmí být v budoucnosti.",
                    new[] { new RecordValidationIssue("ManualActualKroky", "Datum skutečnosti kroku nesmí být v budoucnosti.", "schedule", "manual.future-date", krok.AbsolutniDatum.Value.ToString("yyyy-MM-dd")) },
                    "ManualProposalFieldValidator.ValidateManualActualKroky: future date");
            }
        }
    }

    /// <summary>
    /// FIX 2026-05-01 (round 2 #12) — pre-check že KrokKey patří mezi manuální {2,5,8,9}.
    /// Volitelná validace pro callers, kteří mají schema mapu KrokKey→KrokPoradi.
    /// Defense-in-depth: <see cref="ManualActualKrokApplier.Compute"/> také checkuje,
    /// ale errorová hláška z Compute je generic. Tady pre-check vyhodí user-friendly RVE.
    /// </summary>
    public static void ValidateManualKrokKeysAreManualOnly(
        IReadOnlyList<ManualActualKrokDto> manualKroky,
        IReadOnlyDictionary<Guid, int> krokKeyToPoradi)
    {
        foreach (var mk in manualKroky)
        {
            if (!krokKeyToPoradi.TryGetValue(mk.KrokKey, out var poradi))
            {
                throw new RecordValidationException(
                    "Krok harmonogramu pro zadaný identifikátor neexistuje ve schématu záznamu.",
                    new[] { new RecordValidationIssue("ManualActualKroky", "Neznámý krok ve schématu záznamu.", "schedule", "manual.unknown-krok", mk.KrokKey.ToString()) },
                    $"ValidateManualKrokKeysAreManualOnly: KrokKey={mk.KrokKey} nenalezen ve schématu");
            }

            if (!HarmonogramManualSteps.IsManual(poradi))
            {
                throw new RecordValidationException(
                    $"Krok {poradi} je v Auto rezimu — ručně vyplnit lze jen kroky 2, 5, 8 a 9.",
                    new[] { new RecordValidationIssue("ManualActualKroky", $"Krok {poradi} není manuální (jen 2/5/8/9 jsou manuální).", "schedule", "manual.auto-step", poradi.ToString()) },
                    $"ValidateManualKrokKeysAreManualOnly: KrokPoradi={poradi} není v HarmonogramManualSteps");
            }
        }
    }

    /// <summary>
    /// Pre-bound vazby bublina-&gt;krok (schéma 3 — CREATE_RECORD):
    /// - <c>KrokKey</c> nesmí být <see cref="Guid.Empty"/>.
    /// - <c>ExterniOdkazIndex</c> musí být v rozsahu [0, externiVazbyCount).
    /// - <c>HotVyjadreniId</c> musí být kladné.
    /// - Jeden krok = max jedna bublina (žádné duplikáty KrokKey).
    /// </summary>
    /// <summary>
    /// Phase 6 (DESIGN-5-A + 7-A, 2026-05-01) — odmítne payload obsahující DELAY hodnoty
    /// pro auto-fillované kroky. Auto kroky (1/3/4 PMP, 1/6/7/10 PNF) se plní automaticky
    /// ze SD vyjádření a nelze je ani upravit napřímo, ani navrhnout úpravu.
    /// Vynucuje invariant: Auto rezim ↔ návrh = mutuálně výlučné stavy.
    ///
    /// Submit + Approve obě volají tento validátor (defense-in-depth).
    /// </summary>
    /// <param name="actualHodnoty">payload.ActualHarmonogramHodnoty navrhovaného harmonogramu.</param>
    /// <param name="autoFilledDelayTypIds">Set TypId DELAY řádků auto-fillovaných pro typ záznamu.</param>
    public static void ValidateAutoStepNotInProposal(
        IReadOnlyList<SaveRecordHarmonogramValueCommand> actualHodnoty,
        IReadOnlySet<int> autoFilledDelayTypIds)
    {
        if (actualHodnoty.Count == 0 || autoFilledDelayTypIds.Count == 0)
        {
            return;
        }

        foreach (var item in actualHodnoty)
        {
            if (autoFilledDelayTypIds.Contains(item.TypId))
            {
                // FIX 2026-05-01 (#7): RecordValidationException pro user-friendly message.
                var msg = $"Krok pro TypId {item.TypId} je v Auto rezimu — návrh úpravy skutečnosti není možný. " +
                          "Použij přímou editaci s ToggleRezim=Manual mimo návrhový workflow, nebo edituj jen manuální kroky 2/5/8/9.";
                throw new RecordValidationException(
                    msg,
                    new[] { new RecordValidationIssue("HarmonogramHodnoty", msg, "schedule", "schedule.auto-step-rejected", item.TypId.ToString()) },
                    $"ValidateAutoStepNotInProposal: auto step TypId={item.TypId} v payloadu");
            }
        }
    }

    public static void ValidateHarmonogramVazby(IReadOnlyList<HarmonogramVazbaDto> vazby, int externiVazbyCount)
    {
        if (vazby.Count == 0)
        {
            return;
        }

        // FIX 2026-05-01 (#7): RecordValidationException pro user-friendly message (sjednoceno
        // s ValidateManualActualKroky stejným patternem).
        var seen = new HashSet<Guid>();
        foreach (var v in vazby)
        {
            if (v.KrokKey == Guid.Empty)
            {
                throw new RecordValidationException(
                    "Vazba vyjádření na krok musí mít vyplněný identifikátor kroku.",
                    new[] { new RecordValidationIssue("HarmonogramVazby", "Vazba vyjádření na krok musí mít vyplněný identifikátor kroku.", "schedule", "vazba.krokkey", null) },
                    "ValidateHarmonogramVazby: KrokKey == Empty");
            }

            if (!seen.Add(v.KrokKey))
            {
                throw new RecordValidationException(
                    "Pro jeden krok harmonogramu lze navést jen jednu bublinu.",
                    new[] { new RecordValidationIssue("HarmonogramVazby", "Pro jeden krok harmonogramu lze navést jen jednu bublinu.", "schedule", "vazba.duplicate", v.KrokKey.ToString()) },
                    "ValidateHarmonogramVazby: duplicate KrokKey");
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
