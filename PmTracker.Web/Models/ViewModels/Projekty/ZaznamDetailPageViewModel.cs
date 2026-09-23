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
    public string? PrintPdfUrl { get; set; }
    public string? PrintWordUrl { get; set; }
    public string? BackUrl { get; set; }
}
