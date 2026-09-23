using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-09-17 §6.2 — v ručním režimu UI nabízí datum skutečnosti i u kroků
/// 1/3/4/6/7/10, ale server ho dosud tiše zahazoval (phantom UI). Po opravě se uloží
/// a krok přejde do Manual, takže ho automat přestane přepisovat.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ManualActualAutoEligibleTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ManualActualAutoEligibleTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveRecord_ShouldPersistManualDate_ForAutoEligibleKrok_InManualRezim()
    {
        var db = await _fixture.CreateDatabaseAsync("manual_actual_auto_eligible");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "MANAUTO");

        store.SaveRecord(
            scenario.BuildManualActualCommand(rezim: "Manual", poradi: 4, datum: new DateOnly(2026, 9, 10)),
            scenario.CurrentUser);

        var krok = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum.Should().Be(new DateTime(2026, 9, 10));
        krok.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        krok.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
    }

    [Fact]
    public async Task SaveRecord_ShouldIgnoreManualDate_ForAutoEligibleKrok_InAutoRezim()
    {
        var db = await _fixture.CreateDatabaseAsync("manual_actual_auto_eligible_ignored");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "AUTOIGN");

        var act = () => store.SaveRecord(
            scenario.BuildManualActualCommand(rezim: "Auto", poradi: 4, datum: new DateOnly(2026, 9, 10)),
            scenario.CurrentUser);

        act.Should().NotThrow("v režimu Automatika se odeslaná data jen ignorují, nespadne to");
        var krok = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 4);
        krok.SkutecnostDatum.Should().BeNull("v režimu Automatika krok patří automatu");
    }

    /// <summary>Kroky 2/5/8/9 fungovaly vždy — regrese by je neměla rozbít.</summary>
    [Fact]
    public async Task SaveRecord_ShouldStillPersistManualDate_ForManualKrok()
    {
        var db = await _fixture.CreateDatabaseAsync("manual_actual_manual_krok");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "MANKROK");

        store.SaveRecord(
            scenario.BuildManualActualCommand(rezim: "Auto", poradi: 5, datum: new DateOnly(2026, 9, 11)),
            scenario.CurrentUser);

        var krok = await dbContext.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(k => k.ZaznamId == scenario.RecordId && k.Poradi == 5);
        krok.SkutecnostDatum.Should().Be(new DateTime(2026, 9, 11),
            "manuální kroky 2/5/8/9 jsou uživatelovy bez ohledu na master switch");
    }
}
