using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Integration.DataStore;

/// <summary>
/// Spec 2026-09-17 §4.2 — chování zámku karty proti reálné databázi.
/// SQLite ani InMemory se pro MERGE a HOLDLOCK nedají použít, proto integrační sada.
/// </summary>
[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordEditLockDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordEditLockDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TryAcquire_ShouldSucceed_ForFirstUser()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_first_user");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKFIRST");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        var result = await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);

        result.Acquired.Should().BeTrue();
        result.HolderOsobaId.Should().Be(scenario.OsobaId);
    }

    [Fact]
    public async Task TryAcquire_ShouldFail_ForSecondUser_AndReportHolder()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_second_user");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKSECOND");
        var userB = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LockUserB");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);
        var result = await service.TryAcquireAsync(scenario.RecordId, userB);

        result.Acquired.Should().BeFalse("druhý uživatel se do editace nesmí dostat");
        result.HolderOsobaId.Should().Be(scenario.OsobaId);
        result.HolderSinceUtc.Should().NotBeNull("hláška ukazuje, odkdy se záznam upravuje");
    }

    [Fact]
    public async Task TryAcquire_ShouldBeReentrant_ForSameUser_AndKeepAcquiredAt()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_reentrant");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKREENT");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        var first = await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);
        var second = await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);

        second.Acquired.Should().BeTrue("druhý tab téhož uživatele nesmí zamknout sám sebe");
        second.HolderSinceUtc.Should().Be(first.HolderSinceUtc,
            "čas začátku úprav se při obnovení nesmí posouvat");
    }

    [Fact]
    public async Task TryAcquire_ShouldTakeOver_WhenHeartbeatOlderThanTtl()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_ttl_takeover");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKTTL");
        var userB = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LockTtlUserB");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);
        var zamek = await dbContext.ZaznamEditZamky.SingleAsync(x => x.ZaznamId == scenario.RecordId);
        zamek.HeartbeatAt = DateTime.UtcNow - RecordEditLockService.Ttl - TimeSpan.FromMinutes(1);
        await dbContext.SaveChangesAsync();

        var result = await service.TryAcquireAsync(scenario.RecordId, userB);

        result.Acquired.Should().BeTrue("po vypršení TTL zámek přebírá další uživatel");
        result.HolderOsobaId.Should().Be(userB);
    }

    [Fact]
    public async Task Heartbeat_ShouldRefreshOwnLock_AndIgnoreForeignOne()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_heartbeat");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKHB");
        var userB = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LockHbUserB");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);
        var stale = DateTime.UtcNow - TimeSpan.FromMinutes(10);
        await dbContext.ZaznamEditZamky
            .Where(x => x.ZaznamId == scenario.RecordId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.HeartbeatAt, stale));

        await service.HeartbeatAsync(scenario.RecordId, userB);
        var afterForeign = await dbContext.ZaznamEditZamky.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == scenario.RecordId);
        afterForeign.HeartbeatAt.Should().BeCloseTo(stale, TimeSpan.FromSeconds(1),
            "cizí heartbeat nesmí prodlužovat zámek jiného uživatele");

        await service.HeartbeatAsync(scenario.RecordId, scenario.OsobaId);
        var afterOwn = await dbContext.ZaznamEditZamky.AsNoTracking()
            .SingleAsync(x => x.ZaznamId == scenario.RecordId);
        afterOwn.HeartbeatAt.Should().BeAfter(stale);
    }

    [Fact]
    public async Task Release_ShouldRemoveOwnLock_AndLeaveForeignOneAlone()
    {
        var db = await _fixture.CreateDatabaseAsync("lock_release");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var scenario = await RecordConcurrencyScenario.SeedAsync(dbContext, store, "LOCKREL");
        var userB = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "LockRelUserB");
        var service = new RecordEditLockService(dbContext, TimeProvider.System);

        await service.TryAcquireAsync(scenario.RecordId, scenario.OsobaId);

        await service.ReleaseAsync(scenario.RecordId, userB);
        (await dbContext.ZaznamEditZamky.AsNoTracking().AnyAsync(x => x.ZaznamId == scenario.RecordId))
            .Should().BeTrue("cizí zámek nelze uvolnit");

        await service.ReleaseAsync(scenario.RecordId, scenario.OsobaId);
        (await dbContext.ZaznamEditZamky.AsNoTracking().AnyAsync(x => x.ZaznamId == scenario.RecordId))
            .Should().BeFalse("vlastní zámek se po uložení uvolní");
    }
}
