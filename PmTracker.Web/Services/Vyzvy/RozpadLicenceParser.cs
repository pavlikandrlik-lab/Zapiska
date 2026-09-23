using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace PmTracker.Web.Services.Vyzvy;

/// <summary>
/// Čte HOT_KALKULACE.rozpad_licence — HTML tabulku „název | cena", kterou píše ServiceDesk
/// (spec 2026-09-10 B5). Regulárním výrazem, ne XML parserem: stará aplikace píše atributy
/// v apostrofech a HTML entity (&amp;ndash;), na kterých XDocument padá.
/// Nečitelný vstup vrací prázdný seznam — builder pak tiskne jeden řádek s cena_l.
/// </summary>
public static partial class RozpadLicenceParser
{
    public sealed record Polozka(string Nazev, decimal Cena);

    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");

    [GeneratedRegex(@"<tr\b[^>]*>(?<radek>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RadekRegex();

    [GeneratedRegex(@"<td\b[^>]*>(?<bunka>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex BunkaRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex ZnackaRegex();

    public static IReadOnlyList<Polozka> Parse(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return Array.Empty<Polozka>();

        var polozky = new List<Polozka>();
        foreach (Match radek in RadekRegex().Matches(html))
        {
            var bunky = BunkaRegex().Matches(radek.Groups["radek"].Value)
                .Select(b => Text(b.Groups["bunka"].Value))
                .ToArray();
            if (bunky.Length < 2) continue;

            var nazev = bunky[0];
            if (nazev.Length == 0 || !TryCena(bunky[^1], out var cena)) continue;

            polozky.Add(new Polozka(nazev, cena));
        }

        return polozky;
    }

    private static string Text(string bunka)
    {
        var bezZnacek = WebUtility.HtmlDecode(ZnackaRegex().Replace(bunka, string.Empty));
        return string.Join(' ', bezZnacek.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool TryCena(string text, out decimal cena)
    {
        // char.IsWhiteSpace pokrývá i nezlomitelnou mezeru (oddělovač tisíců v cs-CZ).
        var cista = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
        return decimal.TryParse(cista, NumberStyles.Number, Cs, out cena)
            || decimal.TryParse(cista, NumberStyles.Number, CultureInfo.InvariantCulture, out cena);
    }
}
