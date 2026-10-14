using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>
/// db_upgrade_1_4_10 — datum, ke kterému změna stavu platí, a vazba na jednání
/// (spec 2026-10-13 §5).
/// </summary>
public sealed class StatusEffectiveDateMigrationRegistrationTests
{
    private const string MigrationFile = "db_upgrade_1_4_10_zmena_stavu_plati_od.sql";

    [Fact]
    public void Migrace_JeVBootstrapSeznamu_AVKontrolnimSkriptu()
    {
        File.ReadAllText(ResolvePath("docs/technical/06-database-bootstrap-migrations.md"))
            .Should().Contain(MigrationFile);
        File.ReadAllText(ResolvePath("db_check_applied_upgrades.sql"))
            .Should().Contain("db_upgrade_1_4_10_zmena_stavu_plati_od");
    }

    [Fact]
    public void Migrace_HlidaVerziServeru_APrevadiUtcNaMistniDatum_VTransakci()
    {
        var sql = File.ReadAllText(ResolvePath(MigrationFile));

        sql.Should().Contain("SET XACT_ABORT ON").And.Contain("BEGIN TRANSACTION").And.Contain("COMMIT TRANSACTION");
        sql.Should().Contain("ProductMajorVersion", "AT TIME ZONE vyžaduje SQL Server 2016+");
        sql.Should().Contain("AT TIME ZONE ''Central Europe Standard Time''",
            "staré řádky mají datum_zmeny v UTC, plati_od je místní datum");
        sql.Should().Contain("plati_od DATE NOT NULL");
        sql.Should().Contain("FK_zaznam_historie_stavu_zaznamu_jednani");
        sql.Should().Contain("IX_zaznam_historie_stavu_zaznamu_jednani_id");
    }

    [Fact]
    public void StartovaciKontrola_VyzadujeObaSloupce()
    {
        SqlStartupValidatorHostedService.StatusEffectiveDateColumns.Should().BeEquivalentTo(new[]
        {
            ("dbo.zaznam_historie_stavu_zaznamu", "plati_od"),
            ("dbo.zaznam_historie_stavu_zaznamu", "jednani_id"),
        });
    }

    [Theory]
    [InlineData(nameof(ZaznamHistorieStavuZaznamuEntity.PlatiOd), "plati_od")]
    [InlineData(nameof(ZaznamHistorieStavuZaznamuEntity.JednaniId), "jednani_id")]
    public void Entity_JeNamapovanaNaSloupec(string property, string column)
    {
        var prop = HistoryEntityType().FindProperty(property);

        prop.Should().NotBeNull();
        prop!.GetColumnName().Should().Be(column);
    }

    // InMemory provider nemá relační typové mapování (GetColumnType() by spadlo), čte se anotace.
    [Fact]
    public void PlatiOd_JeDatumBezCasu()
        => HistoryEntityType().FindProperty(nameof(ZaznamHistorieStavuZaznamuEntity.PlatiOd))!
            .FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value.Should().Be("date");

    private static Microsoft.EntityFrameworkCore.Metadata.IEntityType HistoryEntityType()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new PmTrackerDbContext(opts);
        return db.Model.FindEntityType(typeof(ZaznamHistorieStavuZaznamuEntity))!;
    }
}
