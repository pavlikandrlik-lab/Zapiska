namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Per-krok stav harmonogramu (datum-model). Viz spec
/// <c>docs/specs/harmonogram-plan-vs-skutecnost.md</c>.
/// </summary>
public enum HarmonogramKrokStav
{
    /// <summary>Nevyplněno a plánované datum je v budoucnu (≥ dnes).</summary>
    Ceka = 0,

    /// <summary>Nevyplněno a plánované datum už ulpynulo (&lt; dnes).</summary>
    VProdleni = 1,

    /// <summary>Skutečnost vyplněna.</summary>
    Splneno = 2,
}
