using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// db_upgrade_1_4_9 (uživatel 2026-10-14): po smazání historických dat záznamu v automatickém
/// režimu krok doplní automat. Harvest automat spouští jen při NOVÉM vyjádření, proto aplikace
/// při startu doplní prázdné kroky, ke kterým už navázané vyjádření existuje.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class HarmonogramAutomatDoplneniTests
{
    private static readonly DateTime Vyjadreni = new(2026, 9, 20);
    private readonly SqlIntegrationFixture _fixture;

    public HarmonogramAutomatDoplneniTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private static async Task<int> UkolAsync(PmTrackerDbContext db, string marker, SkutecnostRezimEnum rezimAutoKroku,
        DateTime? krok6 = null, SkutecnostZdrojEnum zdroj6 = SkutecnostZdrojEnum.Neznamo)
    {
        var osobaId = await IntegrationTestHelper.EnsurePersonAsync(db, marker + "Os");
        var projektId = await IntegrationTestHelper.EnsureProjectAsync(db, marker);
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(db, marker + "_SUB", osobaId);
        var zaznamId = await IntegrationTestHelper.EnsureRecordAsync(db, projektId, osobaId, subsystemId, "U", marker);
        foreach (var poradi in new byte[] { 1, 3, 4, 6, 7, 10 })
        {
            db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
            {
                ZaznamId = zaznamId,
                Poradi = poradi,
                SkutecnostDatum = poradi == 6 ? krok6 : null,
                SkutecnostZdroj = poradi == 6 ? (byte)zdroj6 : (byte)SkutecnostZdrojEnum.Neznamo,
                SkutecnostRezim = (byte)rezimAutoKroku,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
        await VyjadreniVazbaSeed.SeedPnfVazbaSVyjadrenimAsync(db, zaznamId, poradi: 6, datum: Vyjadreni);
        return zaznamId;
    }

    [Fact]
    public async Task DoplnAsync_DoplniJenPrazdneKrokyZaznamuVAutomatickemRezimu()
    {
        var db = await _fixture.CreateDatabaseAsync("harmonogram_automat_doplneni");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var prazdny = await UkolAsync(dbContext, "DOPLAUTO", SkutecnostRezimEnum.Auto);
        var rucni = await UkolAsync(dbContext, "DOPLRUCN", SkutecnostRezimEnum.Manual);
        var vyplneny = await UkolAsync(dbContext, "DOPLVYPL", SkutecnostRezimEnum.Auto, Vyjadreni, SkutecnostZdrojEnum.Automat);
        var sync = new HarmonogramSkutecnostSyncService(
            dbContext, new PevnyTypTiketuVyjadreniQueryService("PNF"), TimeProvider.System,
            NullLogger<HarmonogramSkutecnostSyncService>.Instance);

        var pocet = await HarmonogramAutomatDoplneniHostedService.DoplnAsync(dbContext, sync, CancellationToken.None);

        pocet.Should().Be(1, "jen záznam v automatickém režimu s prázdným krokem, ke kterému je vyjádření");
        var krok = (int zaznamId) => dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(k => k.ZaznamId == zaznamId && k.Poradi == 6);
        var doplneny = await krok(prazdny);
        doplneny.SkutecnostDatum.Should().Be(Vyjadreni);
        doplneny.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
        (await krok(rucni)).SkutecnostDatum.Should().BeNull("v ručním režimu automat nic nedoplňuje");
        (await krok(vyplneny)).SkutecnostDatum.Should().Be(Vyjadreni);

        (await HarmonogramAutomatDoplneniHostedService.DoplnAsync(dbContext, sync, CancellationToken.None))
            .Should().Be(0, "druhý běh nemá co doplnit");
    }
}
