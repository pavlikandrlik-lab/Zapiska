namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Semantické varianty pro pm-badge (gov-tag).
/// </summary>
public enum PmBadgeVariant
{
    /// <summary>Neutrální štítek (šedá) — výchozí.</summary>
    Neutral,
    /// <summary>Primární akcent (modrá).</summary>
    Primary,
    /// <summary>Pozitivní stav (zelená).</summary>
    Success,
    /// <summary>Upozornění (oranžová).</summary>
    Warning,
    /// <summary>Negativní stav nebo chyba (červená).</summary>
    Error
}
