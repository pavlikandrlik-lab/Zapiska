namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Semantické varianty pro pm-alert.
/// </summary>
public enum PmAlertVariant
{
    /// <summary>Informativní zpráva (modrá).</summary>
    Info,
    /// <summary>Úspěšná akce (zelená).</summary>
    Success,
    /// <summary>Upozornění vyžadující pozornost (oranžová).</summary>
    Warning,
    /// <summary>Chyba nebo zásadní problém (červená).</summary>
    Error
}
