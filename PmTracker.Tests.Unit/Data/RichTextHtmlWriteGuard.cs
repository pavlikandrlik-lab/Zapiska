using System.Text.RegularExpressions;

namespace PmTracker.Tests.Unit.Data;

/// <summary>
/// Hledá zápisy formátovaného HTML (popis záznamu, text vyjádření, požadavek do výzvy), které
/// obcházejí háček čistého textu v PmTrackerDbContext (RichTextSearchTextSync). Po takovém
/// zápisu zůstane čistý text zastaralý a hledání tiše vrací staré výsledky.
/// - V kódu aplikace je každý přímý zápis HTML chyba — patří přes EF.
/// - V upgrade skriptu po 1_4_7 musí příkaz, který mění HTML, ve stejném příkazu nastavit
///   odpovídající *_prosty_text na NULL (docs/technical/06-database-bootstrap-migrations.md, 5.7).
/// Rozbor je po příkazech (oddělené „;“ nebo GO), ne plný SQL parser — raději přísnější:
/// falešný poplach se řeší úpravou detektoru, přehlédnutý zápis ne.
/// </summary>
internal static class RichTextHtmlWriteGuard
{
    private static readonly (string Table, string Html, string Plain)[] Columns =
    {
        ("projektove_zaznamy", "popis", "popis_prosty_text"),
        ("vyjadreni", "text_vyjadreni", "text_vyjadreni_prosty_text"),
        ("zaznam_externi_odkazy", "pozadavek", "pozadavek_prosty_text"),
    };

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex StatementSeparator = new(@";|^\s*GO\s*$", Options | RegexOptions.Multiline);

    private static readonly Regex WriteVerb = new(@"\b(UPDATE|MERGE)\b", Options);

    // SET klauzule (i „UPDATE SET“ v MERGE) až po WHERE/FROM/OUTPUT/WHEN nebo konec příkazu.
    private static readonly Regex SetClause = new(
        @"\bSET\b(?<body>.*?)(?=\bWHERE\b|\bFROM\b|\bOUTPUT\b|\bWHEN\b|$)", Options | RegexOptions.Singleline);

    // Přiřazení v SET: volitelný alias, volitelné hranaté závorky; ne ==, <=, >=, !=.
    private static readonly Regex Assignment = new(@"(?:\[?\w+\]?\.)?\[?(?<col>\w+)\]?\s*=(?!=)", Options);

    // EF ExecuteUpdate nad HTML vlastností (PopisProstyText apod. nevadí — \b za názvem).
    private static readonly Regex EfHtmlSetProperty = new(
        @"ExecuteUpdate\w*\(.*?SetProperty\(\s*\w+\s*=>\s*\w+\.(Popis|TextVyjadreni|Pozadavek)\b",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex MigrationVersion = new(@"db_upgrade_(\d+)_(\d+)(?:_(\d+))?_", Options);

    /// <summary>Příkazy, které zapisují HTML mimo EF (pravidlo pro kód aplikace).</summary>
    public static IEnumerable<string> FindRawHtmlWrites(string source)
        => Statements(source).Where(s => EfHtmlSetProperty.IsMatch(s) || AssignedHtmlColumns(s).Any());

    /// <summary>Příkazy upgrade skriptu, které mění HTML a nevynulují čistý text.</summary>
    public static IEnumerable<string> FindHtmlWritesWithoutPlainReset(string sql)
        => Statements(sql).Where(s =>
        {
            var setBodies = SetBodies(s);
            return AssignedHtmlColumns(s).Any(c => !setBodies.Any(body =>
                Regex.IsMatch(body, $@"\[?\b{c.Plain}\b\]?\s*=\s*NULL\b", Options)));
        });

    /// <summary>Upgrade skripty novější než 1_4_7 — starší běžely, než čistý text existoval.</summary>
    public static bool IsMigrationAfterPlainTextColumns(string fileName)
    {
        var match = MigrationVersion.Match(Path.GetFileName(fileName));
        if (!match.Success)
        {
            return false;
        }

        var version = (Major: int.Parse(match.Groups[1].Value), Minor: int.Parse(match.Groups[2].Value),
            Patch: match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0);
        return version.CompareTo((1, 4, 7)) > 0;
    }

    private static IEnumerable<string> Statements(string source)
        => StatementSeparator.Split(source).Select(s => s.Trim()).Where(s => s.Length > 0);

    private static List<string> SetBodies(string statement)
        => SetClause.Matches(statement).Select(m => m.Groups["body"].Value).ToList();

    private static IEnumerable<(string Table, string Html, string Plain)> AssignedHtmlColumns(string statement)
    {
        if (!WriteVerb.IsMatch(statement))
        {
            return Enumerable.Empty<(string, string, string)>();
        }

        var assigned = SetBodies(statement)
            .SelectMany(body => Assignment.Matches(body).Select(m => m.Groups["col"].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Columns.Where(c =>
            assigned.Contains(c.Html) && Regex.IsMatch(statement, $@"\b{c.Table}\b", Options));
    }
}
