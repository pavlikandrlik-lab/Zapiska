namespace PmTracker.Web.TagHelpers;

/// <summary>
/// Sdílený enum pro velikost pm-* komponent.
/// Mapuje se v každém TagHelperu na gov atribut size="s|m|l".
/// </summary>
public enum PmComponentSize
{
    Small,
    Medium,
    Large
}

internal static class PmComponentSizeExtensions
{
    public static string ToGovAttribute(this PmComponentSize size) => size switch
    {
        PmComponentSize.Small => "s",
        PmComponentSize.Large => "l",
        _ => "m"
    };
}
