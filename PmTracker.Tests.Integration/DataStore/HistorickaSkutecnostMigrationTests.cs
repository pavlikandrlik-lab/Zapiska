using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Common;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// db_upgrade_1_4_9 (uživatel 2026-10-14): historická skutečnost (zdroj 3) se převede podle
/// režimu záznamu — v ručním režimu na ruční, v automatickém se u kroků 1/3/4/6/7/10 smaže
/// (doplní ji automat). Kroky 2/5/8/9 jsou ruční vždy. Analytický skript musí ukázat přesně to,
/// co výkonný skript udělá.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class HistorickaSkutecnostMigrationTests
{
    private const string Analyza = "db_upgrade_1_4_9_historicka_skutecnost_analyza.sql";
    private const string Migrace = "db_upgrade_1_4_9_historicka_skutecnost.sql";
    private static readonly DateTime D3 = new(2026, 9, 10);
    private static readonly DateTime D4 = new(2026, 9, 11);
    private static readonly DateTime D5 = new(2026, 9, 12);

    private readonly SqlIntegrationFixture _fixture;

    public HistorickaSkutecnostMigrationTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private static async Task<int> UkolAsync(PmTrackerDbContext db, string marker, byte rezimAutoKroku,
        params (int Poradi, DateTime? Datum, SkutecnostZdrojEnum Zdroj, SkutecnostRezimEnum? Rezim)[] kroky)
    {
        var osobaId = await IntegrationTestHelper.EnsurePersonAsync(db, marker + "Os");
        var projektId = await IntegrationTestHelper.EnsureProjectAsync(db, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(db, marker + "_SUB", osobaId);
        var zaznamId = await IntegrationTestHelper.EnsureRecordAsync(db, projektId, osobaId, subsystemId, "U", marker);
        for (var poradi = 1; poradi <= 10; poradi++)
        {
            var upraveny = kroky.FirstOrDefault(k => k.Poradi == poradi);
            var rucniKrok = poradi is 2 or 5 or 8 or 9;
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = zaznamId,
                Poradi = (byte)poradi,
                SkutecnostDatum = upraveny.Poradi == poradi ? upraveny.Datum : null,
                SkutecnostZdroj = upraveny.Poradi == poradi ? (byte)upraveny.Zdroj : (byte)SkutecnostZdrojEnum.Neznamo,
                SkutecnostRezim = upraveny.Poradi == poradi && upraveny.Rezim.HasValue
                    ? (byte)upraveny.Rezim.Value
                    : rucniKrok ? (byte)SkutecnostRezimEnum.Manual : rezimAutoKroku,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return zaznamId;
    }

    private static async Task<List<(int ZaznamId, int Krok, string AkceKod)>> AnalyzaAsync(string connectionString)
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(RepositoryPaths.Root, Analyza));
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var radky = new List<(int, int, string)>();
        while (await reader.ReadAsync())
        {
            radky.Add((reader.GetInt32(reader.GetOrdinal("zaznam_id")),
                       Convert.ToInt32(reader["krok"]),
                       reader.GetString(reader.GetOrdinal("akce_kod"))));
        }
        return radky;
    }

    private static Task<ZaznamHarmonogramKrokEntity> KrokAsync(PmTrackerDbContext db, int zaznamId, int poradi)
        => db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(k => k.ZaznamId == zaznamId && k.Poradi == poradi);

    [Fact]
    public async Task Analyza_UkazeAkciProKazdyHistorickyKrok_AMigraceJiProvede_Idempotentne()
    {
        var db = await _fixture.CreateDatabaseAsync("historicka_skutecnost");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);

        // Ruční režim záznamu; krok 4 má (smíšený stav) režim Auto — i ten se převede na ruční.
        var rucni = await UkolAsync(dbContext, "HISTRUC", (byte)SkutecnostRezimEnum.Manual,
            (3, D3, SkutecnostZdrojEnum.Historicka, null),
            (4, D4, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Auto),
            (5, D5, SkutecnostZdrojEnum.Historicka, null));
        // Automatický režim záznamu; krok 4 má hodnotu od automatu, krok 6 historický zdroj bez data.
        var auto = await UkolAsync(dbContext, "HISTAUT", (byte)SkutecnostRezimEnum.Auto,
            (3, D3, SkutecnostZdrojEnum.Historicka, null),
            (4, D4, SkutecnostZdrojEnum.Automat, null),
            (5, D5, SkutecnostZdrojEnum.Historicka, null),
            (6, null, SkutecnostZdrojEnum.Historicka, null));

        var analyza = await AnalyzaAsync(db.ConnectionString);

        analyza.Should().BeEquivalentTo(new[]
        {
            (rucni, 3, "RUCNI"),
            (rucni, 4, "RUCNI"),
            (rucni, 5, "RUCNI"),
            (auto, 3, "SMAZAT"),
            (auto, 5, "RUCNI"),
            (auto, 6, "ZDROJ")
        });

        await SqlScriptRunner.ExecuteScriptsAsync(db.ConnectionString, [Path.Combine(RepositoryPaths.Root, Migrace)]);

        foreach (var poradi in new[] { 3, 4, 5 })
        {
            var k = await KrokAsync(dbContext, rucni, poradi);
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual, $"ruční režim: krok {poradi} je ruční");
            k.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual, "automat ruční krok nesmí přepsat");
            k.SkutecnostDatum.Should().NotBeNull();
        }

        var a3 = await KrokAsync(dbContext, auto, 3);
        a3.SkutecnostDatum.Should().BeNull("automatický režim: historické datum se smaže");
        a3.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
        a3.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Auto);
        (await KrokAsync(dbContext, auto, 4)).SkutecnostDatum.Should().Be(D4, "data automatu se nemění");
        var a5 = await KrokAsync(dbContext, auto, 5);
        a5.SkutecnostDatum.Should().Be(D5, "kroky 2/5/8/9 jsou ruční vždy");
        a5.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
        (await KrokAsync(dbContext, auto, 6)).SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);

        (await dbContext.ZaznamHarmonogramKroky.AsNoTracking().CountAsync(k => k.SkutecnostZdroj == (byte)SkutecnostZdrojEnum.Historicka))
            .Should().Be(0);
        (await AnalyzaAsync(db.ConnectionString)).Should().BeEmpty("po migraci už není co převádět");

        // Druhé spuštění nic nezmění.
        await SqlScriptRunner.ExecuteScriptsAsync(db.ConnectionString, [Path.Combine(RepositoryPaths.Root, Migrace)]);
        (await KrokAsync(dbContext, rucni, 3)).SkutecnostDatum.Should().Be(D3);
        (await KrokAsync(dbContext, auto, 5)).SkutecnostDatum.Should().Be(D5);
    }
}
