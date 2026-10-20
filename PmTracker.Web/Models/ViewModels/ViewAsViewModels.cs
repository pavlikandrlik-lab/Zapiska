namespace PmTracker.Web.Models.ViewModels;

/// <summary>Kdo se v režimu „Zobrazit jako“ skutečně dívá. Null v kontextu = žádný pohled.</summary>
public sealed record ViewAsInfo(int RealOsobaId, string RealDisplayName);

public sealed record ViewAsBarViewModel(string TargetDisplayName, string RealDisplayName);

public sealed class ViewAsModalViewModel
{
    public required string SearchUrl { get; init; }
}
