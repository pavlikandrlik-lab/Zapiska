namespace PmTracker.Web.Models.ViewModels;

/// <summary>
/// Shared input model for meeting tiles rendered in multiple screens.
/// Centralizing this contract keeps tile actions/labels consistent.
/// </summary>
public sealed class MeetingCardViewModel
{
    /// <summary>Project context used by delete action.</summary>
    public int ProjektId { get; init; }
    /// <summary>Controls visibility of delete button.</summary>
    public bool CanDelete { get; init; }
    /// <summary>Chooses between AJAX delete (project tab) and classic post (meetings index).</summary>
    public bool UseAjaxDelete { get; init; }
    /// <summary>Optional return URL for non-AJAX delete flow.</summary>
    public string? ReturnUrl { get; init; }
    /// <summary>Navigation target for whole card click.</summary>
    public required string DetailUrl { get; init; }
    /// <summary>PDF/print endpoint bound to format chooser.</summary>
    public required string PrintPdfUrl { get; init; }
    /// <summary>Word export endpoint bound to format chooser.</summary>
    public required string PrintWordUrl { get; init; }
    /// <summary>Meeting payload rendered by the shared tile.</summary>
    public required JednaniListItemViewModel Jednani { get; init; }
}
