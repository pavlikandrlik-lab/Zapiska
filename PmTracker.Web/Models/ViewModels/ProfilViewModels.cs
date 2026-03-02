namespace PmTracker.Web.Models.ViewModels;

public sealed class ProfilPageViewModel
{
    public required CurrentUserContextViewModel Uzivatel { get; init; }
    public required EffectivePermissionsPreviewViewModel MojePrava { get; init; }
}
