using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>db_upgrade_1_4_6 — rich text v Unicode a bez entit pro písmena.</summary>
public sealed class RichTextUnicodeMigrationRegistrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_6_richtext_unicode.sql";

    [Fact]
    public void Migrace_JeZaregistrovanaVBootstrapSeznamu()
    {
        // RepositoryPaths.GetBootstrapScripts parsuje tenhle seznam pro Testcontainers.
        var doc = File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"));

        doc.Should().Contain(MigrationFile);
    }

    [Fact]
    public void Migrace_JeVKontrolnimSkriptu()
    {
        var check = File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"));

        check.Should().Contain("db_upgrade_1_4_6_richtext_unicode");
    }

    [Fact]
    public void Migrace_BeziVTransakci_AJeOchranenaProtiPreskoceniPredchozichSkriptu()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));

        sql.Should().Contain("SET XACT_ABORT ON");
        sql.Should().Contain("BEGIN TRANSACTION");
        sql.Should().Contain("COMMIT TRANSACTION");
        sql.Should().Contain("COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek')",
            "pozadavek zakládá 1_4_2 — bez něj má skript skončit srozumitelnou chybou");
    }
}
