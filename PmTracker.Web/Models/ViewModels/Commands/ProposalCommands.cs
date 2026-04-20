using System.ComponentModel.DataAnnotations;

namespace PmTracker.Web.Models.ViewModels;

public sealed class ProposalDecisionCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ProposalId { get; set; }
}

public sealed class PrefillCreateProposalCommand
{
    [Required]
    public int ProjektId { get; set; }

    [Required]
    public int ProposalId { get; set; }

    public string? Presentation { get; set; }

    public string? ReturnUrl { get; set; }
}
