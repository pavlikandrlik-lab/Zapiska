using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Architecture guard pro variantu (a) refactoringu DeleteRecord (2026-04-27).
///
/// Po refactoru je cleanup child tabulek delegován na SQL Server přes
/// FK ON DELETE CASCADE. Aplikační logika (DeleteRecordAsync) už nemaže ručně
/// 13 tabulek — jen načte audit snapshots a smaže parent record.
///
/// Tento test chrání invariant: <b>každá nová child tabulka přidaná v budoucnosti
/// musí mít v SQL migration ON DELETE CASCADE (nebo explicitní výjimku v allowlistu)</b>.
/// Pokud by někdo přidal novou tabulku s FK NO ACTION na projektove_zaznamy a
/// neaktualizoval DeleteRecord, hard-delete by selhal s UNEXPECTED_SERVER_ERROR.
///
/// Test prochází db_upgrade_*.sql + initial seed (záznamy jednání-7.sql) a hledá
/// FOREIGN KEY definice na projektove_zaznamy. Pro každou kontroluje, že bud':
///   (a) má ON DELETE CASCADE v aktuální definici, NEBO
///   (b) je v allowlistu (ALLOWED_NON_CASCADE_FK) — speciální případy se zdůvodněním.
///
/// Allowlist je explicit a změna vyžaduje úpravu testu (= peer review checkpoint).
/// </summary>
public sealed class RecordDeleteCascadeFkTests
{
    /// <summary>
    /// FK na projektove_zaznamy které nemají ON DELETE CASCADE — explicitně povolené.
    /// Format: "(child_table).(column_name)" → reason
    /// </summary>
    private static readonly Dictionary<string, string> AllowedNonCascadeFk = new(StringComparer.OrdinalIgnoreCase)
    {
        // SET NULL — preserve audit historie proposalů které tento záznam vytvořily.
        // Multi-path avoidance vůči FK_zaznam_navrhy_zaznam (CASCADE).
        ["zaznam_navrhy.approved_record_id"] = "SET NULL — audit preservation + multi-path avoidance",
    };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
    }

    [Fact]
    public void EveryFkOnProjektoveZaznamyMustBeCascadeOrAllowlisted()
    {
        var repoRoot = FindRepoRoot();

        // Migration db_upgrade_1_3_12 je zdroj pravdy — nahrazuje seed FK definice.
        var migrationPath = Path.Combine(repoRoot, "db_upgrade_1_3_12_record_delete_cascade.sql");
        File.Exists(migrationPath).Should().BeTrue(
            "migration soubor db_upgrade_1_3_12_record_delete_cascade.sql musí existovat — to je zdroj CASCADE definic.");

        var migrationText = File.ReadAllText(migrationPath);

        // Hledáme ALL `FK_*_zaznam` constraints + ALL `FK_*_approved_record` v migration.
        // Každý takový FK musí mít buď CASCADE nebo být v allowlistu.
        // Format v migrationu:
        //   ALTER TABLE dbo.{table}
        //       ADD CONSTRAINT FK_{name}
        //       FOREIGN KEY ({column}) REFERENCES dbo.projektove_zaznamy(id) ON DELETE {ACTION};
        var fkPattern = new Regex(
            @"ALTER TABLE dbo\.(?<table>\w+)\s+ADD CONSTRAINT (?<fkname>FK_\w+)\s+FOREIGN KEY \((?<column>\w+)\) REFERENCES dbo\.projektove_zaznamy\(id\)(?:\s+ON DELETE (?<action>\w+(?:\s+\w+)?))?",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var matches = fkPattern.Matches(migrationText);

        matches.Count.Should().BeGreaterThan(10,
            "migration musí obsahovat ALTER TABLE ... ADD CONSTRAINT FK_xxx FOREIGN KEY ... REFERENCES projektove_zaznamy(id) " +
            "pro každou child tabulku s FK na projektove_zaznamy. Aktuálně jich je 14.");

        var violations = new List<string>();
        foreach (Match match in matches)
        {
            var table = match.Groups["table"].Value;
            var column = match.Groups["column"].Value;
            var action = match.Groups["action"].Success ? match.Groups["action"].Value.Trim() : "NO ACTION";

            var key = $"{table}.{column}";
            var isCascade = string.Equals(action, "CASCADE", StringComparison.OrdinalIgnoreCase);
            var isAllowed = AllowedNonCascadeFk.ContainsKey(key);

            if (!isCascade && !isAllowed)
            {
                violations.Add($"  - {key}: ON DELETE {action} (musí být CASCADE nebo v allowlistu se zdůvodněním)");
            }
        }

        violations.Should().BeEmpty(
            $"FK na projektove_zaznamy musí mít CASCADE (nebo být v allowlistu).\n" +
            $"Pokud přidáváš novou child tabulku, použij ON DELETE CASCADE v migration.\n" +
            $"Pokud opravdu potřebuješ jiné chování, přidej entry do AllowedNonCascadeFk se zdůvodněním.\n" +
            $"Porušení:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void ExterniOdkazyHarvestFkMustRemainNoActionDueToMultiCascadePath()
    {
        // 2026-04-28 lessons learned (SQL 1785 multi-cascade-path):
        // FK_zhvv_externi_odkaz REFERENCES zaznam_externi_odkazy ON DELETE NO ACTION
        // je úmyslně ZACHOVÁN. Pokus o změnu na CASCADE (db_upgrade_1_3_13) selhal s
        // SQL Server error 1785 (CRTFKINVTOPO):
        //
        //   "Introducing FOREIGN KEY constraint may cause cycles or multiple
        //    cascade paths."
        //
        // Důvod: vyjadreni_vazby má DVA FK na projektove_zaznamy:
        //   1) přímo přes FK_zhvv_zaznam (zaznam_id, CASCADE z 1_3_6)
        //   2) přes zaznam_externi_odkazy.zaznam_id (CASCADE z 1_3_12)
        //                  → FK_zhvv_externi_odkaz (externi_odkaz_id, NO ACTION = jediná možná)
        //
        // SQL Server vyžaduje single-path cascade graph. Druhá CASCADE cesta by porušila
        // tento constraint. Migration 1_3_13 byla odstraněna 2026-04-28.
        //
        // User požadavek "delete externí vazby je běžná operace" se řeší
        // application-side cleanup v ReplaceRecordExternalLinksAsync (EF Core
        // RemoveRange vyjadreni_vazby PŘED RemoveRange externí vazby + SaveChanges
        // respektuje FK ordering).
        var repoRoot = FindRepoRoot();
        var sourceFile = Path.Combine(repoRoot, "db_upgrade_1_3_6_vyjadreni_vazba.sql");
        File.Exists(sourceFile).Should().BeTrue();

        var text = File.ReadAllText(sourceFile);
        text.Should().Contain("FK_zhvv_externi_odkaz REFERENCES dbo.zaznam_externi_odkazy(id) ON DELETE NO ACTION",
            "FK z vyjadreni_vazby na zaznam_externi_odkazy musí být NO ACTION — multi-cascade-path constraint (SQL 1785). Pokud potřebuješ jiné chování, řeš v aplikaci, ne v SQL.");

        // Migration 1_3_13 NESMÍ existovat (rolled back po SQL 1785).
        var failedMigration = Path.Combine(repoRoot, "db_upgrade_1_3_13_externi_odkaz_cascade.sql");
        File.Exists(failedMigration).Should().BeFalse(
            "Migration db_upgrade_1_3_13_externi_odkaz_cascade.sql byla odstraněna — způsobila SQL 1785 multi-cascade-path violation.");
    }
}
