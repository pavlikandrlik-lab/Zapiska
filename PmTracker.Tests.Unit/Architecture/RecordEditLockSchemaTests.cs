using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Spec 2026-09-17 §4.1 — mapování zámku karty a jeho zapojení do nasazovací řady.
/// Bootstrap testovacích databází se řídí seznamem v dokumentaci, takže migrace,
/// která tam chybí, by se do testovacích DB vůbec nedostala.
/// </summary>
public sealed class RecordEditLockSchemaTests
{
    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    [Fact]
    public void ZaznamEditZamek_JeNamapovanNaSpravnouTabulku()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new PmTrackerDbContext(opts);

        var entityType = db.Model.FindEntityType(typeof(ZaznamEditZamekEntity));

        entityType.Should().NotBeNull("bez mapování by zámek nešlo číst ani zapisovat");
        entityType!.GetTableName().Should().Be("zaznam_edit_zamek");
        entityType.FindPrimaryKey()!.Properties.Single().Name
            .Should().Be(nameof(ZaznamEditZamekEntity.ZaznamId), "jeden zámek na záznam");
    }

    [Fact]
    public void Migrace_1_4_5_Existuje_A_JeIdempotentni()
    {
        var path = Path.Combine(LocateRepoRoot(), "db_upgrade_1_4_5_record_edit_lock.sql");

        File.Exists(path).Should().BeTrue("schéma řídí ruční skripty, EF Migrations se nepoužívají");
        var sql = File.ReadAllText(path);
        sql.Should().Contain("IF OBJECT_ID(N'dbo.zaznam_edit_zamek', N'U') IS NULL",
            "skript musí jít spustit opakovaně — operátor ho pouští ručně");
    }

    [Fact]
    public void Migrace_1_4_5_JeVBootstrapSeznamu()
    {
        var doc = File.ReadAllText(Path.Combine(
            LocateRepoRoot(), "docs", "technical", "06-database-bootstrap-migrations.md"));

        doc.Should().Contain("db_upgrade_1_4_5_record_edit_lock.sql",
            "RepositoryPaths.GetBootstrapScripts čte pořadí skriptů z tohoto seznamu — "
            + "bez zápisu by tabulka v testovacích databázích nevznikla");
    }

    [Fact]
    public void Migrace_1_4_5_MaOtiskVDiagnostice()
    {
        var check = File.ReadAllText(Path.Combine(LocateRepoRoot(), "db_check_applied_upgrades.sql"));

        check.Should().Contain("db_upgrade_1_4_5_record_edit_lock",
            "operátor musí na produkci poznat, že skript ještě neběžel");
    }

    /// <summary>
    /// Klíč je id záznamu, ne IDENTITY. Bez explicitního ValueGeneratedNever by konvence EF
    /// označila celočíselný PK za store-generated a zápis zámku přes EF by se rozešel se schématem.
    /// </summary>
    [Fact]
    public void ZaznamEditZamek_MaKlicKteryNegenerujeDatabaze()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new PmTrackerDbContext(opts);

        var key = db.Model.FindEntityType(typeof(ZaznamEditZamekEntity))!
            .FindProperty(nameof(ZaznamEditZamekEntity.ZaznamId))!;

        key.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never);
    }

    /// <summary>
    /// Bez tabulky spadne otevření editoru až za běhu. Startovní guard ji musí vyžadovat
    /// a hláška musí pojmenovat skript, který to spraví — operátor ji kopíruje do sqlcmd.
    /// </summary>
    [Fact]
    public void StartovniGuard_VyzadujeTabulkuZamku_APojmenujeSkript()
    {
        var validator = File.ReadAllText(Path.Combine(
            LocateRepoRoot(), "PmTracker.Web", "Services", "Data", "SqlStartupValidatorHostedService.cs"));

        validator.Should().Contain("dbo.zaznam_edit_zamek",
            "deploy bez migrace musí selhat při startu, ne až při otevření editoru");
        validator.Should().Contain("db_upgrade_1_4_5_record_edit_lock.sql",
            "hláška pro operátora musí jít zkopírovat a spustit");
    }
}
