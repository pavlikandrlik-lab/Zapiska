using System.Net;
using System.Text.RegularExpressions;

namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Převod HTML obsahu HOT_VYJADRENI.popis na čitelný plain text pro chat modal.
/// ServiceDesk ukládá vyjádření jako HTML snippet (legacy ASP aplikace),
/// ale PM Tracker chat bubliny chceme renderovat jako plain text s line breaks
/// — renderování cizího HTML přes Html.Raw by bylo XSS risk + stejně se v chatu
/// netrefí žádnému účelu. Zachováváme jen newlines (přes whitespace-pre-wrap v CSS).
/// </summary>
public static class VyjadreniHtmlText
{
    // br varianty (case-insensitive, s atributy / selfclose) → newline
    private static readonly Regex BrRegex = new(@"<br\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // li open tag → newline + bullet (konec li ignorujeme)
    private static readonly Regex LiOpenRegex = new(@"<li\s*[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Jakýkoli zbylý tag → smazat (včetně atributů i přes řádky)
    private static readonly Regex AnyTagRegex = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);

    // Více po sobě jdoucích whitespace / newlines → max 2 newlines (paragraph break)
    private static readonly Regex MultiWhitespaceRegex = new(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex MultiNewlineRegex = new(@"(\r?\n){3,}", RegexOptions.Compiled);

    /// <summary>
    /// Převede HTML popis na plain text. Vrací prázdný string pro null vstup.
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // 1) <br> → \n
        var text = BrRegex.Replace(html, "\n");

        // 2) <li ...> → \n• (zbylé </li> smaže AnyTagRegex)
        text = LiOpenRegex.Replace(text, "\n• ");

        // 3) Ostatní tagy pryč
        text = AnyTagRegex.Replace(text, string.Empty);

        // 4) HTML entity dekóduj (&nbsp;, &amp;, &#34;, &lt; atd.)
        text = WebUtility.HtmlDecode(text);

        // 5) &nbsp; se po HtmlDecode stává   (non-breaking space) — sjednotit na běžný space
        text = text.Replace(' ', ' ');

        // 6) Normalizace whitespace — víc mezer na 1, víc než 2 newlines na 2
        text = MultiWhitespaceRegex.Replace(text, " ");
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = MultiNewlineRegex.Replace(text, "\n\n");

        return text.Trim();
    }
}
