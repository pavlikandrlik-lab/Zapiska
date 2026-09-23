namespace PmTracker.Web.Models.ViewModels.Vyzvy;

/// <summary>
/// Popisky a barvy stavů výzvy. Sdílené railem i panelem, aby se texty nerozešly —
/// dřív žily jen v kartě výzvy a s jejím smazáním zmizely.
/// </summary>
public static class VyzvaStavPresentation
{
    /// <summary>Barva pro gov-tag. Hodnoty odpovídají paletě gov design systému.</summary>
    public static string Color(string stav) => stav switch
    {
        "Priprava" => "warning",
        "Odeslano" => "success",
        _ => "neutral"
    };

    public static string Label(string stav) => stav switch
    {
        "Priprava" => "Příprava",
        "Odeslano" => "Odesláno",
        _ => "Zrušeno"
    };
}
