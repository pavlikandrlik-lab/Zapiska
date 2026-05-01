using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

/// <summary>
/// Regression test pro DeleteRecord refactor — varianta (a) SQL CASCADE (2026-04-27).
///
/// Historie:
/// - 2026-04-27 ráno: Bug — DeleteRecord nemazal vyjadreni_vazby ani zaznam_navrhy,
///   FK NO ACTION způsobovalo UNEXPECTED_SERVER_ERROR. Fix přidal manuální cleanup.
/// - 2026-04-27 odpoledne: Refactor (varianta a) — manuální cleanup chain všech
///   13 tabulek delegován na SQL FK CASCADE (db_upgrade_1_3_12). Aplikace nemaže
///   ručně, jen načte audit snapshots, smaže parent record, SaveChanges.
///
/// Tento test chrání invariant nového stavu:
///   1. DeleteRecord NEsmí ručně volat RemoveRange na child tabulkách
///      (cleanup je SQL CASCADE responsibility).
///   2. Audit snapshot fetches MUSÍ být AsNoTracking (jinak EF tracking konflikty
///      po SQL CASCADE).
///   3. Pouze parent record (ProjektoveZaznamy) se Remove.
///
/// Pokud někdo v budoucnu přidá novou child tabulku BEZ ON DELETE CASCADE v SQL
/// migration, hard-delete selže UNEXPECTED_SERVER_ERROR — chytí to architecture
/// guard test <c>RecordDeleteCascadeFkTests</c>.
/// </summary>
public sealed class RecordServiceDeleteCleanupTests
{
    private static string LoadDeleteRecordSource()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře.");
        }

        var path = Path.Combine(dir.FullName, "PmTracker.Web", "Services", "RecordService.DeleteRecord.cs");
        File.Exists(path).Should().BeTrue($"soubor musí existovat: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void DeleteRecordAsync_MustNotManuallyRemoveRangeChildEntities()
    {
        // Po variantě (a) refactor SQL CASCADE handlu cleanup.
        // RemoveRange volní na child tabulkách = anti-pattern (delegace na SQL FK).
        var source = LoadDeleteRecordSource();

        var forbiddenRemoveRangePatterns = new[]
        {
            "ZaznamHistorieZmenTypu.RemoveRange",
            "ZaznamHistorieTerminu.RemoveRange",
            "ZaznamHistorieVlastnik.RemoveRange",
            "ZaznamHistorieSubsystem.RemoveRange",
            "ZaznamHistorieStavuZaznamu.RemoveRange",
            "ZaznamHistorieStavuProjektu.RemoveRange",
            "ZaznamExterniOdkazy.RemoveRange",
            "VyjadreniVazby.RemoveRange",
            "ZaznamSpoluprace.RemoveRange",
            "ZaznamHarmonogramHodnoty.RemoveRange",
            "Vyjadreni.RemoveRange",
            "ZaznamPriorityUzivatelu.RemoveRange",
            "ZaznamNavrhy.RemoveRange"
        };

        var violations = forbiddenRemoveRangePatterns
            .Where(p => source.Contains(p))
            .ToList();

        violations.Should().BeEmpty(
            "DeleteRecordAsync nesmí ručně mazat child tabulky — to je SQL CASCADE odpovědnost po variantě (a) refactoringu (2026-04-27). " +
            $"Nalezené ruční cleanup volní: {string.Join(", ", violations)}. " +
            "Pokud potřebuješ ruční cleanup, znamená to, že SQL FK na dotyčné tabulce nemá ON DELETE CASCADE — fix v db_upgrade migraci, ne v aplikaci.");
    }

    [Fact]
    public void DeleteRecordAsync_MustOnlyRemoveParentRecord()
    {
        // Pouze parent ProjektoveZaznamy.Remove je očekávaný — vše ostatní cascade.
        var source = LoadDeleteRecordSource();

        source.Should().Contain("ProjektoveZaznamy.Remove(record)",
            "DeleteRecordAsync musí volat ProjektoveZaznamy.Remove(record) — to triggeruje SQL CASCADE.");

        // Žádný jiný .Remove(...) ani RemoveRange(...) na DbSetech kromě ProjektoveZaznamy
        var removeCallPattern = new Regex(@"\.(Remove|RemoveRange)\(");
        var matches = removeCallPattern.Matches(source);

        // Allowed: dbContext.ProjektoveZaznamy.Remove(record)
        // (žádné jiné Remove/RemoveRange volní v souboru)
        var nonParentRemoves = matches.Count - 1;
        nonParentRemoves.Should().Be(0,
            "DeleteRecordAsync smí volat .Remove() pouze 1× — na ProjektoveZaznamy.Remove(record). " +
            "Cleanup ostatních tabulek je SQL CASCADE odpovědnost.");
    }

    [Fact]
    public void DeleteRecordAsync_MustFetchAuditSnapshotsAsNoTracking()
    {
        // Po DELETE parent + SQL CASCADE budou child rows smazané. Pokud byly načtené
        // s default tracking, EF context obsahuje stale tracked entity references —
        // potenciální zdroj concurrency exceptions při následných SaveChanges nebo
        // queries v tom samém context. AsNoTracking() to eliminuje.
        var source = LoadDeleteRecordSource();

        // Required AsNoTracking pro audit fetches (Vyjadreni a ZaznamHarmonogramHodnoty)
        source.Should().MatchRegex(@"ZaznamHarmonogramHodnoty[\s\S]{0,200}AsNoTracking",
            "ZaznamHarmonogramHodnoty audit fetch musí být AsNoTracking.");
        source.Should().MatchRegex(@"Vyjadreni[\s\S]{0,200}AsNoTracking",
            "Vyjadreni audit fetch musí být AsNoTracking.");
    }

    [Fact]
    public void DeleteRecordAsync_FileSizeShouldBeReasonable()
    {
        // Po refactoru by mělo být <= ~110 řádků. Pokud roste, znamená to, že někdo
        // přidává zpět manuální cleanup — fix v SQL migraci, ne v kódu.
        var source = LoadDeleteRecordSource();
        var lineCount = source.Split('\n').Length;

        lineCount.Should().BeLessThan(120,
            "DeleteRecordAsync by měl být tenký — SQL CASCADE handle cleanup. Pokud LOC roste, někdo přidává manuální cleanup zpět.");
    }
}
