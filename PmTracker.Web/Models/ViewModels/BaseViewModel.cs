namespace PmTracker.Web.Models.ViewModels;

public abstract class BaseViewModel
{
    public CurrentUserContextViewModel CurrentUserContext { get; set; } = null!;

    public bool CanViewPeopleTab =>
        CurrentUserContext.HasPermissionPrefix(PermissionKeys.PeoplePrefix);

    public bool CanViewCiselnikyTab =>
        CurrentUserContext.HasPermissionPrefix(PermissionKeys.CiselnikyPrefix);

    public bool CanViewSettingsTab =>
        CurrentUserContext.HasPermissionPrefix(PermissionKeys.SettingsPrefix);
}
