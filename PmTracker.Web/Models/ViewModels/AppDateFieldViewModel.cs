namespace PmTracker.Web.Models.ViewModels;

public sealed class AppDateFieldViewModel
{
    public required string Name { get; init; }
    public string IsoValue { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public bool Locked { get; init; }
    public string AriaLabel { get; init; } = "Otevřít kalendář";
    public string? ContainerCssClass { get; init; }
    public IReadOnlyDictionary<string, object?> ExtraDataAttributes { get; init; }
        = new Dictionary<string, object?>();
}
