namespace PmTracker.Web.Models.ViewModels;

public sealed class NavPermissionsViewModel
{
    public bool CanViewPeople { get; init; }
    public bool CanViewCiselniky { get; init; }
    public bool CanViewSettings { get; init; }
    public string? CurrentUserDisplayName { get; init; }
    public string? CurrentUserEmail { get; init; }
    public string? CurrentUserOrg { get; init; }
    public string? CurrentUserOrgCode { get; init; }
    public IReadOnlyList<string> CurrentUserRoles { get; init; } = Array.Empty<string>();
}
