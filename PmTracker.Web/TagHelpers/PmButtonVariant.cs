namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Semantické varianty pro pm-button — nezávislé na gov atributech.
/// Mapování na gov-button color/type je centralizované v PmButtonTagHelperu.
/// </summary>
public enum PmButtonVariant
{
    /// <summary>Primární akce (uložit, potvrdit, odeslat).</summary>
    Primary,
    /// <summary>Sekundární akce (zrušit, zpět).</summary>
    Secondary,
    /// <summary>Destruktivní akce (smazat, odstranit).</summary>
    Destructive,
    /// <summary>Tiché akce (toggle, link-like button).</summary>
    Ghost
}
