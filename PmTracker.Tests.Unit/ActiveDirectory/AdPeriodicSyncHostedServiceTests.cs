using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ActiveDirectory;

public sealed class AdPeriodicSyncHostedServiceTests
{
    private static IServiceProvider BuildProvider(IActiveDirectoryService adStub, TimeProvider time, string dbName)
    {
        var services = new ServiceCollection();
        services.AddSingleton(time);
        services.AddDbContext<PmTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IActiveDirectoryService>(_ => adStub);
        services.AddScoped<IAdSyncService, AdSyncService>();
        services.AddSingleton(typeof(ISyncJobRunLock<>), typeof(SyncJobRunLock<>));
        services.AddSingleton(typeof(ManualTriggerSignal<>));
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ExecuteTickAsync_WritesLastRunAtAndLastResultJson()
    {
        var fakeNow = new DateTimeOffset(2026, 4, 22, 14, 0, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(fakeNow);

        var adMock = new Mock<IActiveDirectoryService>();
        adMock.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ActiveDirectoryBatchResponse
              {
                  Available = true,
                  Persons = Array.Empty<ActiveDirectoryPersonResult>(),
                  NotFoundGuids = Array.Empty<Guid>()
              });

        var dbName = "ad-periodic-" + Guid.NewGuid();
        var sp = BuildProvider(adMock.Object, time, dbName);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            db.AdSyncSettings.Add(new AdSyncSettingsEntity
            {
                Id = 1,
                IsEnabled = true,
                PeriodMinutes = 60,
                AnchorAt = fakeNow.AddHours(-1),
                LastRunAt = null
            });
            await db.SaveChangesAsync();
        }

        var sut = new AdPeriodicSyncHostedService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ISyncJobRunLock<AdSyncSettingsEntity>>(),
            sp.GetRequiredService<ManualTriggerSignal<AdSyncSettingsEntity>>(),
            time,
            NullLogger<AdPeriodicSyncHostedService>.Instance);

        await sut.ExecuteTickAsync(SyncTriggerKind.Manual, CancellationToken.None);

        using var verifyScope = sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
        var after = await verifyDb.AdSyncSettings.AsNoTracking().FirstAsync(x => x.Id == 1);
        after.LastRunAt.Should().NotBeNull();
        after.LastTriggerKind.Should().Be("manual");
        after.LastResultJson.Should().NotBeNullOrWhiteSpace();
        after.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteTickAsync_ReturnsFalseWhenAlreadyRunning()
    {
        var time = new FakeTimeProvider();
        var adMock = new Mock<IActiveDirectoryService>();
        var dbName = "ad-periodic-running-" + Guid.NewGuid();
        var sp = BuildProvider(adMock.Object, time, dbName);

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            db.AdSyncSettings.Add(new AdSyncSettingsEntity { Id = 1, IsEnabled = true, PeriodMinutes = 60, AnchorAt = time.GetUtcNow() });
            await db.SaveChangesAsync();
        }

        var runLock = sp.GetRequiredService<ISyncJobRunLock<AdSyncSettingsEntity>>();
        runLock.TryAcquire().Should().BeTrue();
        try
        {
            var sut = new AdPeriodicSyncHostedService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                runLock,
                sp.GetRequiredService<ManualTriggerSignal<AdSyncSettingsEntity>>(),
                time,
                NullLogger<AdPeriodicSyncHostedService>.Instance);

            var outcome = await sut.ExecuteTickAsync(SyncTriggerKind.Manual, CancellationToken.None);

            outcome.Should().BeFalse();
        }
        finally
        {
            runLock.Release();
        }
    }
}
