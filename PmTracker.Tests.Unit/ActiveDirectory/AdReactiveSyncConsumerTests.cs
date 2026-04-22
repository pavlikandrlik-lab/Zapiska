using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ActiveDirectory;

public sealed class AdReactiveSyncConsumerTests
{
    [Fact]
    public async Task EnqueuedRequest_TriggersSyncSinglePersonAsync()
    {
        var guid = Guid.NewGuid();
        var dbName = "ad-reactive-" + Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<PmTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(typeof(IReactiveSyncQueue<>), typeof(ReactiveSyncQueue<>));

        var adMock = new Mock<IActiveDirectoryService>();
        adMock.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new ActiveDirectoryBatchResponse
              {
                  Available = true,
                  Persons = new[]
                  {
                      new ActiveDirectoryPersonResult
                      {
                          GuidAd = guid,
                          DisplayName = "Updated",
                          Jmeno = "Upd",
                          Prijmeni = "Ated",
                          Email = "upd@example.com",
                          CanSelect = true
                      }
                  },
                  NotFoundGuids = Array.Empty<Guid>()
              });
        services.AddScoped<IActiveDirectoryService>(_ => adMock.Object);
        services.AddScoped<IAdSyncService, AdSyncService>();
        services.AddLogging();

        var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            db.Osoby.Add(new OsobaEntity
            {
                Id = 10,
                Jmeno = "Old",
                Prijmeni = "Name",
                Email = "old@example.com",
                GuidAd = guid,
                OrganizaceId = 1
            });
            await db.SaveChangesAsync();
        }

        var queue = sp.GetRequiredService<IReactiveSyncQueue<AdReactiveSyncRequest>>();
        var consumer = new AdReactiveSyncConsumer(
            queue,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AdReactiveSyncConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await consumer.StartAsync(cts.Token);
        await queue.EnqueueAsync(new AdReactiveSyncRequest(10, AdReactiveSource.ManualUpdate), cts.Token);

        // Poll dokud osoba neupdatuje
        var updated = false;
        for (int i = 0; i < 30 && !cts.IsCancellationRequested; i++)
        {
            await Task.Delay(100, cts.Token);
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PmTrackerDbContext>();
            var after = await db.Osoby.AsNoTracking().FirstAsync(x => x.Id == 10);
            if (after.Jmeno == "Upd")
            {
                updated = true;
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);
        updated.Should().BeTrue("reactive consumer měl osobu aktualizovat z AD stubu");
    }
}
