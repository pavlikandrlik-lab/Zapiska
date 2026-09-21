using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Migrace 1_4_4 uklízí po zrušené indexové vrstvě vyhledávání (spec 2026-09-17 §5).
/// Původní 1_4_4 tabulku SearchIndex naopak zakládala — nebyla nikdy nasazena,
/// proto se číslo recykluje místo zavedení 1_4_5.
/// </summary>
public sealed class SearchMigrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_4_search_cleanup.sql";

    private static string Sql() => File.ReadAllText(ResolvePath(MigrationFile));

    [Fact]
    public void StaraMigrace_ZakladajiciSearchIndex_JizNeexistuje()
    {
        File.Exists(ResolvePath("db_upgrade_1_4_4_search_index.sql")).Should().BeFalse(
            "indexová tabulka se ruší, zakládací skript nesmí zůstat v řadě migrací");
    }

    [Fact]
    public void Migrace_RusiObeTabulkyIndexoveVrstvy()
    {
        var sql = Sql();

        sql.Should().Contain("DROP TABLE dbo.search_reindex_checkpoint");
        sql.Should().Contain("DROP TABLE dbo.SearchIndex");
    }

    [Fact]
    public void Migrace_RusiKlicSearchReindex_AleNechavaSearchIndex()
    {
        var sql = Sql();

        sql.Should().Contain("search.reindex", "klíč hlídal endpoint, který zaniká");

        // search.index drží všech 12 rolí a vyhledávání zůstává — nesmí ho odnést
        // žádný DELETE. Zmínka v kontrolním SELECTu je naopak žádoucí: administrátor
        // z ní pozná, že klíč migraci přežil.
        foreach (var statement in DeleteStatements(sql))
        {
            statement.Should().NotContain("search.index",
                "search.index drží všech 12 rolí a vyhledávání zůstává — klíč se NESMÍ mazat");
        }

        sql.Should().NotContain("permission_categories",
            "kategorie SEARCH neosiřela (drží ji search.index), na rozdíl od vzoru 1_4_1");
    }

    /// <summary>Vrátí každý DELETE příkaz od klíčového slova po nejbližší středník.</summary>
    private static IEnumerable<string> DeleteStatements(string sql)
    {
        var index = 0;
        while ((index = sql.IndexOf("DELETE", index, StringComparison.Ordinal)) >= 0)
        {
            var end = sql.IndexOf(';', index);
            yield return end < 0 ? sql[index..] : sql[index..end];
            index += "DELETE".Length;
        }
    }

    [Fact]
    public void Migrace_JeIdempotentni()
    {
        var sql = Sql();

        sql.Should().Contain("OBJECT_ID(N'dbo.search_reindex_checkpoint'");
        sql.Should().Contain("OBJECT_ID(N'dbo.SearchIndex'");
    }

    [Fact]
    public void Migrace_JeZaregistrovanaVBootstrapSeznamu()
    {
        // RepositoryPaths.GetBootstrapScripts parsuje tenhle seznam pro Testcontainers.
        // Bez zápisu spadnou Integration i Api testy na chybějící/přebývající schéma.
        var doc = File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"));

        doc.Should().Contain(MigrationFile);
        doc.Should().NotContain("db_upgrade_1_4_4_search_index.sql");
    }

    [Fact]
    public void Migrace_JeVKontrolnimSkriptu()
    {
        var check = File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"));

        check.Should().Contain("db_upgrade_1_4_4_search_cleanup");
    }

    [Fact]
    public void KontrolniSkript_Nehlasi_1_1_5_NatrvaloJakoChybejici()
    {
        // 1_1_5 zavedla search_reindex_checkpoint, kterou 1_4_4 ruší. Sonda na pouhou
        // existenci tabulky by administrátorovi po 1_4_4 navždy tvrdila, že mu chybí
        // migrace, kterou dávno spustil.
        var check = File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"));

        var radek = check.IndexOf("db_upgrade_1_1_5_search_checkpoint", StringComparison.Ordinal);
        radek.Should().BeGreaterThan(-1, "řádek 1_1_5 v kontrolním skriptu musí zůstat");

        var konecRadku = check.IndexOf(");", radek, StringComparison.Ordinal);
        var blok = check[radek..konecRadku];

        blok.Should().Contain("@searchCleanup",
            "verdikt 1_1_5 musí brát v potaz, že tabulku zrušila 1_4_4");
    }
}
