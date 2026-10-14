using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-10-14 — uložení skutečnosti z editoru jediným seznamem HarmonogramHodnoty.
/// Nahrazuje ManualActualAutoEligibleTests (Fáze 1), které posílaly ManualActualKroky.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ScheduleActualSaveTests
{
    private static readonly DateTime A = new(2026, 9, 10);
    private static readonly DateTime B = new(2026, 9, 20);
    private readonly SqlIntegrationFixture _fixture;

    public ScheduleActualSaveTests(SqlIntegrationFixture fixture) => _fixture = fixture;

    private async Task<(PmTrackerDbContext Db, IntegrationTestDataStore Store, RecordConcurrencyScenario S)> SeedAsync(string dbName, string marker)
    {
        var db = await _fixture.CreateDatabaseAsync(dbName);
        var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, marker);
        return (dbContext, store, scenario);
    }

    private static Task<ZaznamHarmonogramKrokEntity> KrokAsync(PmTrackerDbContext db, int recordId, int poradi)
        => db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(k => k.ZaznamId == recordId && k.Poradi == poradi);

    private static async Task SetKrokAsync(PmTrackerDbContext db, int recordId, int poradi, DateTime? datum, SkutecnostZdrojEnum zdroj, SkutecnostRezimEnum rezim)
    {
        var krok = await db.ZaznamHarmonogramKroky.SingleAsync(k => k.ZaznamId == recordId && k.Poradi == poradi);
        krok.SkutecnostDatum = datum;
        krok.SkutecnostZdroj = (byte)zdroj;
        krok.SkutecnostRezim = (byte)rezim;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task SaveRecord_AutoKrokVRucnimRezimu_UlozeniZmenaISmazani()
    {
        var (db, store, s) = await SeedAsync("sched_actual_auto_manual", "SAMAN");
        await using var _ = db;

        store.SaveRecord(s.BuildActualCommand("Manual", 3, A), s.CurrentUser);
        (await KrokAsync(db, s.RecordId, 3)).SkutecnostDatum.Should().Be(A);

        // D2: druhé uložení — krok má zdroj Ruční, změna se musí uložit.
        store.SaveRecord(s.BuildActualCommand("Manual", 3, B, puvodni: A), s.CurrentUser);
        (await KrokAsync(db, s.RecordId, 3)).SkutecnostDatum.Should().Be(B);

        // D2: smazání uloženého data.
        store.SaveRecord(s.BuildActualCommand("Manual", 3, null, puvodni: B), s.CurrentUser);
        var k3 = await KrokAsync(db, s.RecordId, 3);
        k3.SkutecnostDatum.Should().BeNull();
        k3.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
    }

    [Fact]
    public async Task SaveRecord_AutoKrokVAutoRezimu_ZmenuIgnoruje()
    {
        var (db, store, s) = await SeedAsync("sched_actual_auto_ignored", "SAIGN");
        await using var _ = db;

        store.SaveRecord(s.BuildActualCommand("Auto", 4, A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 4)).SkutecnostDatum.Should().BeNull("v automatickém režimu krok řídí automat");
    }

    [Fact]
    public async Task SaveRecord_RucniKrok_UlozeVKazdemRezimu()
    {
        var (db, store, s) = await SeedAsync("sched_actual_manual_step", "SAMKR");
        await using var _ = db;

        store.SaveRecord(s.BuildActualCommand("Auto", 5, A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 5)).SkutecnostDatum.Should().Be(A, "kroky 2/5/8/9 jsou vždy ruční");
    }

    /// <summary>D3 — vypnutí automatiky a uložení bez úprav nesmí smazat data automatu.</summary>
    [Fact]
    public async Task SaveRecord_PrepnutiNaRucniBezUprav_NechaDataAutomatu()
    {
        var (db, store, s) = await SeedAsync("sched_actual_keep_automat", "SAKEEP");
        await using var _ = db;
        await SetKrokAsync(db, s.RecordId, 1, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);
        await SetKrokAsync(db, s.RecordId, 3, A, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);

        // Editor předvyplní hodnotu automatu a stejnou pošle jako původní → žádná změna.
        var command = s.BuildActualCommand("Manual", 1, A, puvodni: A);
        var row3 = command.HarmonogramHodnoty.Single(x => x.Poradi == 3);
        row3.SkutecnostDatum = A;
        row3.SkutecnostDatumPuvodni = A;
        store.SaveRecord(command, s.CurrentUser);

        foreach (var poradi in new[] { 1, 3 })
        {
            var k = await KrokAsync(db, s.RecordId, poradi);
            k.SkutecnostDatum.Should().Be(A);
            k.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
            k.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        }
    }

    /// <summary>Review Focus 1 — automat změnil krok během editace; nedotčený krok nesmí vrátit starou hodnotu.</summary>
    [Fact]
    public async Task SaveRecord_NedotcenyKrok_NechaNovejsiHodnotuAutomatu()
    {
        var (db, store, s) = await SeedAsync("sched_actual_newer_automat", "SANEW");
        await using var _ = db;
        await SetKrokAsync(db, s.RecordId, 4, B, SkutecnostZdrojEnum.Automat, SkutecnostRezimEnum.Auto);

        // Editor se vykreslil, když měl krok 4 ještě hodnotu A.
        store.SaveRecord(s.BuildActualCommand("Manual", 4, A, puvodni: A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 4)).SkutecnostDatum.Should().Be(B);
    }

    /// <summary>R2 + R3 — přepnutí na automatiku smaže ruční i historická data 1/3/4/6/7/10, kroky 2/5/8/9 nechá.</summary>
    [Fact]
    public async Task SaveRecord_PrepnutiNaAuto_SmazeNeautomatovaDataAutoKrokuANechaKroky2589()
    {
        var (db, store, s) = await SeedAsync("sched_actual_to_auto", "SATOAU");
        await using var _ = db;
        await SetKrokAsync(db, s.RecordId, 1, A, SkutecnostZdrojEnum.Historicka, SkutecnostRezimEnum.Manual);
        await SetKrokAsync(db, s.RecordId, 3, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);
        await SetKrokAsync(db, s.RecordId, 5, B, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);

        store.SaveRecord(s.BuildActualCommand("Auto", 3, null, puvodni: A), s.CurrentUser);

        (await KrokAsync(db, s.RecordId, 3)).SkutecnostDatum.Should().BeNull();
        (await KrokAsync(db, s.RecordId, 1)).SkutecnostDatum.Should().BeNull("nic mezi — historická data se mažou také");
        var k5 = await KrokAsync(db, s.RecordId, 5);
        k5.SkutecnostDatum.Should().Be(B);
        k5.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
    }

    /// <summary>
    /// R2 — po přepnutí na automatiku automat krok hned doplní z vyjádření, které už je na
    /// záznamu navázané. Harvest sám sync spouští jen při NOVÉM vyjádření, proto ho uložení
    /// volá přímo (po commitu); harvest se navíc naplánuje kvůli čerstvým datům ze SD.
    /// </summary>
    [Fact]
    public async Task SaveRecord_PrepnutiNaAuto_AutomatHnedDoplniKrokZNavazanehoVyjadreni()
    {
        var db = await _fixture.CreateDatabaseAsync("sched_actual_to_auto_sync");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var harvest = new RecordingHarvestScheduler();
        var store = IntegrationTestHelper.CreateDataStore(dbContext, configureServices: services =>
        {
            services.AddScoped<IVyjadreniQueryService>(_ => new PevnyTypTiketuVyjadreniQueryService("PNF"));
            services.AddSingleton<IHarvestScheduler>(harvest);
        });
        var s = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "SASYNC");
        var linkId = await SeedVazbaSVyjadrenimAsync(dbContext, s.RecordId, poradi: 6, datum: B);
        await SetKrokAsync(dbContext, s.RecordId, 6, A, SkutecnostZdrojEnum.Manual, SkutecnostRezimEnum.Manual);

        var command = s.BuildActualCommand("Auto", 6, A, puvodni: A);
        command.ExterniVazby = [new SaveRecordExterniVazbaCommand { Id = linkId, Typ = "PNF", Cislo = "123456" }];
        store.SaveRecord(command, s.CurrentUser);

        var k6 = await KrokAsync(dbContext, s.RecordId, 6);
        k6.SkutecnostDatum.Should().Be(B, "automat doplní datum z navázaného vyjádření hned po uložení");
        k6.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
        harvest.RecordHarvestCalls.Should().ContainSingle().Which.Should().Be(s.RecordId);
    }

    /// <summary>Uložení záznamu, který už automatický je, není přepnutí — automat ani harvest se nespouští.</summary>
    [Fact]
    public async Task SaveRecord_ZaznamUzVAuto_NespoustiAutomatAniHarvest()
    {
        var db = await _fixture.CreateDatabaseAsync("sched_actual_auto_no_sync");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var harvest = new RecordingHarvestScheduler();
        var store = IntegrationTestHelper.CreateDataStore(dbContext,
            configureServices: services => services.AddSingleton<IHarvestScheduler>(harvest));
        var s = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "SANOSY");

        store.SaveRecord(s.BuildActualCommand("Auto", 5, A), s.CurrentUser);

        harvest.RecordHarvestCalls.Should().BeEmpty();
    }

    /// <summary>Navázané vyjádření, jak ho založí harvest (v provozu VyjadreniHarvestService).</summary>
    private static async Task<int> SeedVazbaSVyjadrenimAsync(PmTrackerDbContext db, int recordId, int poradi, DateTime datum)
    {
        var typId = await db.CiselnikTypuExternichOdkazu.AsNoTracking()
            .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();
        var link = new ZaznamExterniOdkazEntity { ZaznamId = recordId, TypOdkazuId = typId, Cislo = "123456" };
        db.ZaznamExterniOdkazy.Add(link);
        await db.SaveChangesAsync();
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = recordId,
            Poradi = (byte)poradi,
            ExterniOdkazId = link.Id,
            HotVyjadreniId = 987654,
            DatumVyjadreni = datum,
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return link.Id;
    }
}
