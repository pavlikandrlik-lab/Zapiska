using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>db_upgrade_1_4_7 — čistý text formátovaných polí pro hledání (2026-10-08).</summary>
public sealed class ProstyTextMigrationRegistrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_7_prosty_text_hledani.sql";

    // Review I1 (2026-10-08): HTML změněné mimo EF (rollback na <= 1.4.6, budoucí SQL
    // migrace, ruční SSMS oprava) nechá čistý text zastaralý navždy — backfill doplňuje
    // jen NULL. db_reset_prosty_text_hledani.sql je záchranná síť: vynuluje čistý text,
    // další start aplikace ho dopočte znovu. Je to údržbový skript, ne migrace.
    private const string ResetScriptFile = "db_reset_prosty_text_hledani.sql";

    [Fact]
    public void Migrace_JeVBootstrapSeznamu_AVKontrolnimSkriptu()
    {
        File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"))
            .Should().Contain(MigrationFile);
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().Contain("db_upgrade_1_4_7_prosty_text_hledani");
    }

    [Fact]
    public void Migrace_PridaTriSloupce_VTransakci_AHlidaPredchoziSkript()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));

        sql.Should().Contain("SET XACT_ABORT ON").And.Contain("BEGIN TRANSACTION").And.Contain("COMMIT TRANSACTION");
        sql.Should().Contain("popis_prosty_text").And.Contain("text_vyjadreni_prosty_text").And.Contain("pozadavek_prosty_text");
        sql.Should().Contain("COL_LENGTH(N'dbo.zaznam_externi_odkazy', N'pozadavek')",
            "pozadavek zakládá 1_4_2 — bez něj má skript skončit srozumitelnou chybou");
    }

    [Fact]
    public void StartovaciKontrola_VyzadujeVsechnySloupce()
    {
        SqlStartupValidatorHostedService.ProstyTextColumns.Should().BeEquivalentTo(new[]
        {
            ("dbo.projektove_zaznamy", "popis_prosty_text"),
            ("dbo.vyjadreni", "text_vyjadreni_prosty_text"),
            ("dbo.zaznam_externi_odkazy", "pozadavek_prosty_text"),
        });
    }

    [Theory]
    [InlineData(typeof(ProjektovyZaznamEntity), nameof(ProjektovyZaznamEntity.PopisProstyText), "popis_prosty_text")]
    [InlineData(typeof(VyjadreniEntity), nameof(VyjadreniEntity.TextVyjadreniProstyText), "text_vyjadreni_prosty_text")]
    [InlineData(typeof(ZaznamExterniOdkazEntity), nameof(ZaznamExterniOdkazEntity.PozadavekProstyText), "pozadavek_prosty_text")]
    public void Entity_JeNamapovanaNaSloupec(Type entity, string property, string column)
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new PmTrackerDbContext(opts);

        var prop = db.Model.FindEntityType(entity)!.FindProperty(property);
        prop.Should().NotBeNull();
        prop!.GetColumnName().Should().Be(column);
    }

    [Fact]
    public void ResetSkript_Existuje_VynulujeTriSloupce_VTransakci_AHlidaPredchoziSkript()
    {
        var sql = File.ReadAllText(ResolvePath(ResetScriptFile));

        sql.Should().Contain("SET XACT_ABORT ON").And.Contain("BEGIN TRANSACTION").And.Contain("COMMIT TRANSACTION");
        sql.Should().Contain("popis_prosty_text = NULL");
        sql.Should().Contain("text_vyjadreni_prosty_text = NULL");
        sql.Should().Contain("pozadavek_prosty_text = NULL");
        sql.Should().Contain(MigrationFile,
            "guard má operátora poslat nejdřív spustit 1_4_7, než bude mít co vynulovat");
    }

    [Fact]
    public void ResetSkript_JeZminenVDokumentaci_ANeniMigraceVCislovanemSeznamu()
    {
        var bootstrapDoc = File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"));
        var deployDoc = File.ReadAllText(ResolvePath("docs/technical/04-installation-deployment-iis.md"));

        bootstrapDoc.Should().Contain(ResetScriptFile);
        deployDoc.Should().Contain(ResetScriptFile);

        // Údržbový skript, ne migrace — nesmí být mezi číslovanými řádky bootstrap pořadí
        // (tvar "36. `db_upgrade_....sql`"), na kterém jede Testcontainers bootstrap parser.
        Regex.Matches(bootstrapDoc, @"^\d+\.\s*`([^`]+)`", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .Should().NotContain(ResetScriptFile);
    }

    [Fact]
    public void ResetSkript_NeniVKontrolnimSkriptuApliovanychUpgradu()
    {
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().NotContain("db_reset_prosty_text_hledani");
    }
}
