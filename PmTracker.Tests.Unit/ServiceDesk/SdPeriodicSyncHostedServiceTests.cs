using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Integration testy pro SdActivePeriodicSyncHostedService + SdArchivePeriodicSyncHostedService.
/// Ověřují, že periodic tick:
/// - volá IVyjadreniHarvestService.HarvestScopeAsync se správným scope
/// - uloží LastRunAt, LastTriggerKind, LastResultJson na odpovídající settings entitu
/// - respektuje runLock (TryAcquire → neběží → ExecuteTickAsync vrací false)
/// </summary>
public sealed class SdPeriodicSyncHostedServiceTests
{
    private static IServiceProvider BuildProvider(IVyjadreniHarvestService harvest, TimeProvider time, string dbName)
    {
        var services = new ServiceCollection();
        services.AddSingleton(time);
        services.AddDbContext<PmTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped(_ => harvest);
        services.AddSingleton(typeof(ISyncJobRunLock<>), typeof(SyncJobRunLock<>));
        services.AddSingleton(typeof(ManualTriggerSignal<>));
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SdActive_ExecuteTickAsync_CallsHarvestScopeActive_AndPersistsResult()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);

        var harvest = new Mock<IVyjadreniHarvestService>();
        harvest.Setup(h => h.HarvestScopeAsync(HarvestScope.Active, It.IsAny<SyncTriggerKind>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new SdHarvestResult(
                   StartedAt: fakeNow.UtcDateTime,
                   FinishedAt: fakeNow.AddSeconds(1).UtcDateTime,
                   TicketsChecked: 3,
                   TicketsSkippedByFingerprint: 2,
                   TicketsDrilled: 1,
                   BindingsCreated: 1,
                   BindingsUpdated: 0,
                   ErrorCount: 0,
                   Errors: Array.Empty<SdHarvestErrorItem>()));

        var dbName = "sd-active-periodic-" + Guid.NewGuid();
        var sp = BuildProvider(harvest.Object, time, dbName);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            db.SdActiveSyncSettings.Add(new SdActiveSyncSettingsEntity
            {
                Id = 1, IsEnabled = true, PeriodMinutes = 60, AnchorAt = fakeNow.AddHours(-1)
            });
            await db.SaveChangesAsync();
        }

        var sut = new SdActivePeriodicSyncHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ISyncJobRunLock<SdActiveSyncSettingsEntity>>(),
            sp.GetRequiredService<ManualTriggerSignal<SdActiveSyncSettingsEntity>>(),
            time,
            NullLogger<SdActivePeriodicSyncHostedService>.Instance);

        var ok = await sut.ExecuteTickAsync(SyncTriggerKind.Manual, CancellationToken.None);

        ok.Should().BeTrue();
        harvest.Verify(h => h.HarvestScopeAsync(HarvestScope.Active, SyncTriggerKind.Manual, It.IsAny<CancellationToken>()), Times.Once);

        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var after = await verifyDb.SdActiveSyncSettings.AsNoTracking().FirstAsync();
        after.LastRunAt.Should().NotBeNull();
        after.LastTriggerKind.Should().Be("manual");
        after.LastResultJson.Should().NotBeNullOrWhiteSpace();
        after.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task SdArchive_ExecuteTickAsync_CallsHarvestScopeArchive()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);

        var harvest = new Mock<IVyjadreniHarvestService>();
        harvest.Setup(h => h.HarvestScopeAsync(HarvestScope.Archive, It.IsAny<SyncTriggerKind>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(SdHarvestResult.Empty(fakeNow.UtcDateTime));

        var dbName = "sd-archive-periodic-" + Guid.NewGuid();
        var sp = BuildProvider(harvest.Object, time, dbName);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            db.SdArchiveSyncSettings.Add(new SdArchiveSyncSettingsEntity
            {
                Id = 1, IsEnabled = true, PeriodMinutes = 1440, AnchorAt = fakeNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        var sut = new SdArchivePeriodicSyncHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ISyncJobRunLock<SdArchiveSyncSettingsEntity>>(),
            sp.GetRequiredService<ManualTriggerSignal<SdArchiveSyncSettingsEntity>>(),
            time,
            NullLogger<SdArchivePeriodicSyncHostedService>.Instance);

        var ok = await sut.ExecuteTickAsync(SyncTriggerKind.Auto, CancellationToken.None);

        ok.Should().BeTrue();
        harvest.Verify(h => h.HarvestScopeAsync(HarvestScope.Archive, SyncTriggerKind.Auto, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SdActive_ExecuteTickAsync_ReturnsFalse_WhenAlreadyRunning()
    {
        var time = new FakeTimeProvider();
        var harvest = new Mock<IVyjadreniHarvestService>();
        var dbName = "sd-active-running-" + Guid.NewGuid();
        var sp = BuildProvider(harvest.Object, time, dbName);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            db.SdActiveSyncSettings.Add(new SdActiveSyncSettingsEntity
            {
                Id = 1, IsEnabled = true, PeriodMinutes = 60, AnchorAt = time.GetUtcNow()
            });
            await db.SaveChangesAsync();
        }

        var runLock = sp.GetRequiredService<ISyncJobRunLock<SdActiveSyncSettingsEntity>>();
        runLock.TryAcquire().Should().BeTrue();
        try
        {
            var sut = new SdActivePeriodicSyncHostedService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                runLock,
                sp.GetRequiredService<ManualTriggerSignal<SdActiveSyncSettingsEntity>>(),
                time,
                NullLogger<SdActivePeriodicSyncHostedService>.Instance);

            var outcome = await sut.ExecuteTickAsync(SyncTriggerKind.Manual, CancellationToken.None);

            outcome.Should().BeFalse();
            harvest.VerifyNoOtherCalls();
        }
        finally
        {
            runLock.Release();
        }
    }
}
