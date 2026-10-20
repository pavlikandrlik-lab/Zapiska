using System.Text.RegularExpressions;

namespace PmTracker.Web.Services.Security.ViewAs;

/// <summary>
/// Zapisující příkaz = řádek nebo příkaz za středníkem začíná INSERT/UPDATE/DELETE/MERGE/TRUNCATE.
/// Pokrývá EF dávky (SET NOCOUNT ON; INSERT …), ExecuteUpdate/Delete i raw MERGE zámku karty.
/// SELECT s WITH (UPDLOCK) ani sloupce typu datum_update nevyhoví (podtržítko není hranice slova).
/// </summary>
public static partial class SqlWriteCommandClassifier
{
    [GeneratedRegex(
        @"(?:^|;)\s*(?:INSERT|UPDATE|DELETE|MERGE|TRUNCATE)\b",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex WriteStatement();

    public static bool IsWrite(string? commandText)
        => !string.IsNullOrWhiteSpace(commandText) && WriteStatement().IsMatch(commandText);
}
