using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// 2026-10-13: klíč search.index zaniká (hledání hlídá jen přihlášení). Seed ani katalog ho
/// nesmí vracet zpět a DB uklízí idempotentní migrace 1_4_8 podle vzoru 1_4_1.
/// </summary>
public sealed class SearchIndexKeyRemovalTests
{
    private const string MigrationFile = "db_upgrade_1_4_8_drop_search_index.sql";

    [Fact]
    public void Seed_AniKatalog_KlicNeobsahuji()
    {
        File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs"))
            .Should().NotContain("search.index").And.NotContain("\"SEARCH\"");
        File.ReadAllText(ResolvePath("PmTracker.Web/Models/ViewModels/SecurityViewModels.cs"))
            .Should().NotContain("search.index");
    }

    [Fact]
    public void Migrace_MazeKlicVazbyAOsirelouKategorii()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));

        sql.Should().Contain("FROM authz.role_permission_projects");
        sql.Should().Contain("FROM authz.role_permissions");
        sql.Should().Contain("DELETE FROM authz.permissions WHERE klic = N'search.index'");
        sql.Should().Contain("c.kod = N'SEARCH'");
        sql.Should().Contain("NOT EXISTS (SELECT 1 FROM authz.permissions p WHERE p.category_id = c.id)",
            "kategorie se maže jen osiřelá — guard proti cizím klíčům");
        sql.Should().Contain("BEGIN TRANSACTION").And.Contain("COMMIT TRANSACTION");
    }

    [Fact]
    public void Migrace_JeVKontrolnimSkriptu()
    {
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().Contain("db_upgrade_1_4_8_drop_search_index");
    }
}
