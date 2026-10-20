namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// POST akce, která jen čte (náhled, přesměrování na tisk). V pohledu projde,
/// zápis do DB ale dál hlídá EF pojistka.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public sealed class AllowDuringViewAsAttribute : Attribute;

/// <summary>Zahájení / ukončení pohledu — v pohledu projde a smí zapsat audit.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ViewAsControlAttribute : Attribute;
