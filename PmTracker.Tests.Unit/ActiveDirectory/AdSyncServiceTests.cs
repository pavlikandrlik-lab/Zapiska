using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ActiveDirectory;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ActiveDirectory;

public sealed class AdSyncServiceTests
{
    private static PmTrackerDbContext NewInMemory()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("ad-sync-" + Guid.NewGuid())
            .Options;
        return new PmTrackerDbContext(options);
    }

    private static OsobaEntity PersonWithGuid(int id, Guid guid, string email = "old@example.com")
        => new()
        {
            Id = id,
            Jmeno = "Old",
            Prijmeni = "Name",
            Email = email,
            GuidAd = guid,
            OrganizaceId = 1
        };

    [Fact]
    public async Task SyncAllPeopleAsync_UpdatesPeopleWithGuidAd()
    {
        await using var db = NewInMemory();
        var guid = Guid.NewGuid();
        db.Osoby.Add(PersonWithGuid(1, guid));
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = new[]
              {
                  new ActiveDirectoryPersonResult
                  {
                      GuidAd = guid,
                      DisplayName = "New Name",
                      Jmeno = "New",
                      Prijmeni = "Surname",
                      Email = "new@example.com",
                      Company = "Acme",
                      Department = "IT",
                      CanSelect = true
                  }
              },
              NotFoundGuids = Array.Empty<Guid>()
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncAllPeopleAsync(SyncTriggerKind.Auto, CancellationToken.None);

        result.OkCount.Should().Be(1);
        result.ErrorCount.Should().Be(0);
        result.NotFoundInAdCount.Should().Be(0);

        var updated = await db.Osoby.FirstAsync(x => x.Id == 1);
        updated.Jmeno.Should().Be("New");
        updated.Prijmeni.Should().Be("Surname");
        updated.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task SyncAllPeopleAsync_SkipsPeopleWithoutGuidAd()
    {
        await using var db = NewInMemory();
        db.Osoby.Add(new OsobaEntity
        {
            Id = 2,
            Jmeno = "Skip",
            Prijmeni = "Me",
            OrganizaceId = 1,
            GuidAd = null
        });
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = Array.Empty<ActiveDirectoryPersonResult>(),
              NotFoundGuids = Array.Empty<Guid>()
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncAllPeopleAsync(SyncTriggerKind.Auto, CancellationToken.None);

        result.SkippedNoGuidCount.Should().Be(1);
        result.OkCount.Should().Be(0);
    }

    [Fact]
    public async Task SyncAllPeopleAsync_RecordsNotFoundInAdWithoutDeletingPerson()
    {
        await using var db = NewInMemory();
        var guid = Guid.NewGuid();
        db.Osoby.Add(PersonWithGuid(3, guid, "keep@example.com"));
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = Array.Empty<ActiveDirectoryPersonResult>(),
              NotFoundGuids = new[] { guid }
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncAllPeopleAsync(SyncTriggerKind.Auto, CancellationToken.None);

        result.NotFoundInAdCount.Should().Be(1);

        var osoba = await db.Osoby.FirstAsync(x => x.Id == 3);
        osoba.Email.Should().Be("keep@example.com");  // not mutated
    }

    [Fact]
    public async Task SyncSinglePersonAsync_ReturnsFailureWhenOsobaHasNoGuidAd()
    {
        await using var db = NewInMemory();
        db.Osoby.Add(new OsobaEntity { Id = 4, Jmeno = "X", Prijmeni = "Y", OrganizaceId = 1 });
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncSinglePersonAsync(4, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("GuidAd");
    }

    [Fact]
    public async Task SyncSinglePersonAsync_ReturnsNotFoundWhenAdLacksGuid()
    {
        await using var db = NewInMemory();
        var guid = Guid.NewGuid();
        db.Osoby.Add(PersonWithGuid(5, guid));
        await db.SaveChangesAsync();

        var ad = new Mock<IActiveDirectoryService>();
        ad.Setup(x => x.ListByGuidsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new ActiveDirectoryBatchResponse
          {
              Available = true,
              Persons = Array.Empty<ActiveDirectoryPersonResult>(),
              NotFoundGuids = new[] { guid }
          });

        var sut = new AdSyncService(db, ad.Object, NullLogger<AdSyncService>.Instance, TimeProvider.System);

        var result = await sut.SyncSinglePersonAsync(5, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FoundInAd.Should().BeFalse();
    }
}
