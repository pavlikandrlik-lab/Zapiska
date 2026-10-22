namespace PmTracker.Web.Models.ViewModels;

/// <summary>
/// Stránka záznamu (2026-07-14): read-only detail jednoho záznamu na trvalé URL.
/// Skládá existující bloky — hlavičku (summary + detail), vyjádření a obě podoby
/// harmonogramu (grafickou i tabulkovou). Editovatelné je jen přidání vyjádření.
/// </summary>
public sealed class ZaznamDetailPageViewModel
{
    public int ProjektId { get; init; }
    public required string ProjektZkratka { get; init; }
    public required string ProjektNazev { get; init; }

    public required ZaznamCardSummaryViewModel Summary { get; init; }
    public required ZaznamCardDetailViewModel Detail { get; init; }
    public required ZaznamCommentsPanelViewModel Comments { get; init; }

    /// <summary>Grafická podoba (pruhy + osa + rozbalený rozpad). NULL = záznam nemá harmonogram.</summary>
    public ZaznamScheduleBlockViewModel? Schedule { get; init; }

    /// <summary>Tabulková podoba (Krok / Plán / Skutečnost, zamčená). NULL = záznam nemá harmonogram.</summary>
    public HarmonogramBlockViewModel? ScheduleTable { get; init; }

    // Presentation — plní controller.
    public bool CanEditRecord { get; set; }
    public bool CanCreateScheduleProposal { get; set; }
    public string? EditUrl { get; set; }
    public string? ScheduleProposalUrl { get; set; }
    // Task 2 (2026-10-12): endpoint pro ověření dostupnosti PŘED přechodem na editor návrhu
    // (scheduleProposalGuard.js) — čekající návrh ukáže dialog místo chybové stránky.
    public string? ScheduleProposalCheckUrl { get; set; }
    public string? PrintPdfUrl { get; set; }
    public string? PrintWordUrl { get; set; }
    public string? BackUrl { get; set; }

    /// <summary>
    /// Spec 2026-10-22 R10: procházení záznamů seznamu (jednání / aktivní). NULL = stránka bez šipek
    /// (bez parametru seznam, s neplatným nebo cizím seznamem, záznam v seznamu není — R12).
    /// </summary>
    public RecordSequenceNavigationViewModel? Sequence { get; set; }
}

/// <summary>Spec 2026-10-22 R10: „‹ Předchozí · N / M · Další ›“ na stránce záznamu.</summary>
public sealed class RecordSequenceNavigationViewModel
{
    /// <summary>Pozice od 1.</summary>
    public int Position { get; init; }
    public int Count { get; init; }
    public string? PreviousUrl { get; init; }
    public string? PreviousLabel { get; init; }
    public string? NextUrl { get; init; }
    public string? NextLabel { get; init; }
}
