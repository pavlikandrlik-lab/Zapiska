using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Task 6 — fingerprint detekce (spec §5.2) ve VyjadreniHarvestService.
/// Ověřuje:
/// 1) Primary+secondary fingerprint match → skip drill.
/// 2) Primary change, secondary match → skip drill, jen update primary hint.
/// 3) Fingerprint mismatch → drill + persist nový fingerprint.
/// 4) HarvestForRecordAsync = HarvestRecordAsync (enumeruje + harvestuje per odkaz).
/// 5) HarvestScopeAsync(Active) filtruje tickety podle HOT_ZAZNAMY.stav.
/// </summary>
public sealed class FingerprintDetectionTests
{
    private static PmTrackerDbContext InMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }

    private static VyjadreniHarvestService BuildSut(PmTrackerDbContext db, IVyjadreniQueryService vq)
    {
        return new VyjadreniHarvestService(
            db,
            vq,
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<VyjadreniHarvestService>.Instance);
    }

    [Fact]
    public async Task HarvestSingleTicketAsync_PrimaryAndSecondaryMatch_SkipsDrill()
    {
        using var db = InMemoryDb();
        var datum = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 1, ZaznamId = 10, Cislo = "100001",
            LastKnownHotZaznamDatum = datum,
            LastKnownMaxVyjadreniId = 500L,
            LastKnownVyjadreniCount = 3
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
          {
              ["100001"] = new HotZaznamFingerprintDto("100001", datum, "otevreno")
          });
        vq.Setup(q => q.GetVyjadreniSecondaryFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, VyjadreniSecondaryFingerprintDto>
          {
              ["100001"] = new VyjadreniSecondaryFingerprintDto("100001", MaxId: 500L, Count: 3)
          });

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestSingleTicketAsync(externiOdkazId: 1, CancellationToken.None);

        result.Fetched.Should().Be(0);
        result.Message.Should().Contain("Fingerprint");
        vq.Verify(q => q.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HarvestSingleTicketAsync_SecondaryMatchButPrimaryChanged_SkipsDrill_AndUpdatesPrimaryHint()
    {
        using var db = InMemoryDb();
        var oldDatum = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var newDatum = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 1, ZaznamId = 10, Cislo = "100001",
            LastKnownHotZaznamDatum = oldDatum,
            LastKnownMaxVyjadreniId = 500L,
            LastKnownVyjadreniCount = 3
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
          {
              ["100001"] = new HotZaznamFingerprintDto("100001", newDatum, "otevreno")
          });
        vq.Setup(q => q.GetVyjadreniSecondaryFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, VyjadreniSecondaryFingerprintDto>
          {
              ["100001"] = new VyjadreniSecondaryFingerprintDto("100001", MaxId: 500L, Count: 3)
          });

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestSingleTicketAsync(externiOdkazId: 1, CancellationToken.None);

        result.Fetched.Should().Be(0);

        var after = await db.ZaznamExterniOdkazy.AsNoTracking().FirstAsync();
        after.LastKnownHotZaznamDatum.Should().Be(newDatum);  // primary hint updatnutý
        after.LastKnownMaxVyjadreniId.Should().Be(500L);       // secondary beze změny
        vq.Verify(q => q.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HarvestSingleTicketAsync_FingerprintChange_Drills_AndPersistsNewFingerprint()
    {
        using var db = InMemoryDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 10, ProjektId = 1, SubsystemId = 1, KategorieId = 1,
            HarmonogramSablonaVerze = 1, Nazev = "test"
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 1, ZaznamId = 10, Cislo = "100001",
            LastKnownHotZaznamDatum = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc),
            LastKnownMaxVyjadreniId = 500L,
            LastKnownVyjadreniCount = 3
        });
        await db.SaveChangesAsync();

        var newDatum = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
          {
              ["100001"] = new HotZaznamFingerprintDto("100001", newDatum, "otevreno")
          });
        vq.Setup(q => q.GetVyjadreniSecondaryFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, VyjadreniSecondaryFingerprintDto>
          {
              ["100001"] = new VyjadreniSecondaryFingerprintDto("100001", MaxId: 600L, Count: 4)
          });
        vq.Setup(q => q.GetVyjadreniForTicketAsync("100001", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Array.Empty<HotVyjadreniDto>());  // drill returns nothing to bind

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestSingleTicketAsync(externiOdkazId: 1, CancellationToken.None);

        result.Message.Should().BeNull();

        var after = await db.ZaznamExterniOdkazy.AsNoTracking().FirstAsync();
        after.LastKnownHotZaznamDatum.Should().Be(newDatum);
        after.LastKnownMaxVyjadreniId.Should().Be(600L);
        after.LastKnownVyjadreniCount.Should().Be(4);
        after.LastHarvestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HarvestSingleTicketAsync_TicketMissingInHot_ReturnsEmptyWithMessage()
    {
        using var db = InMemoryDb();
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 1, ZaznamId = 10, Cislo = "100001"
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>());  // nic

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestSingleTicketAsync(externiOdkazId: 1, CancellationToken.None);

        result.Message.Should().Contain("neexistuje v HOT_ZAZNAMY");
    }

    [Fact]
    public async Task HarvestForRecordAsync_EnumeratesAllLinksForRecord_AndHarvestsEach()
    {
        using var db = InMemoryDb();
        db.ProjektoveZaznamy.AddRange(
            new ProjektovyZaznamEntity { Id = 99, ProjektId = 1, SubsystemId = 1, KategorieId = 1, HarmonogramSablonaVerze = 1, Nazev = "r99" },
            new ProjektovyZaznamEntity { Id = 88, ProjektId = 1, SubsystemId = 1, KategorieId = 1, HarmonogramSablonaVerze = 1, Nazev = "r88" }
        );
        db.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 99, Cislo = "100001" },
            new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 99, Cislo = "100002" },
            new ZaznamExterniOdkazEntity { Id = 3, ZaznamId = 88, Cislo = "100003" }  // jiný záznam
        );
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);

        await sut.HarvestForRecordAsync(zaznamId: 99, CancellationToken.None);

        vq.Verify(q => q.GetVyjadreniForTicketAsync("100001", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        vq.Verify(q => q.GetVyjadreniForTicketAsync("100002", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        vq.Verify(q => q.GetVyjadreniForTicketAsync("100003", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HarvestScopeAsync_Active_FiltersOutArchiveTickets()
    {
        using var db = InMemoryDb();
        db.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 10, Cislo = "100001" },  // active
            new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 10, Cislo = "100002" }   // archive
        );
        await db.SaveChangesAsync();

        var datum1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
          {
              ["100001"] = new HotZaznamFingerprintDto("100001", datum1, "otevreno"),
              ["100002"] = new HotZaznamFingerprintDto("100002", datum1, "archiv")
          });
        vq.Setup(q => q.GetVyjadreniSecondaryFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, VyjadreniSecondaryFingerprintDto>());

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestScopeAsync(HarvestScope.Active, SyncTriggerKind.Auto, CancellationToken.None);

        // Jen aktivní tiket (100001) je Checked
        result.TicketsChecked.Should().Be(1);
    }

    [Fact]
    public async Task HarvestScopeAsync_Archive_FiltersOutActiveTickets()
    {
        using var db = InMemoryDb();
        db.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 10, Cislo = "100001" },
            new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 10, Cislo = "100002" }
        );
        await db.SaveChangesAsync();

        var datum1 = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
          {
              ["100001"] = new HotZaznamFingerprintDto("100001", datum1, "otevreno"),
              ["100002"] = new HotZaznamFingerprintDto("100002", datum1, "archiv")
          });
        vq.Setup(q => q.GetVyjadreniSecondaryFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, VyjadreniSecondaryFingerprintDto>());

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestScopeAsync(HarvestScope.Archive, SyncTriggerKind.Auto, CancellationToken.None);

        result.TicketsChecked.Should().Be(1);  // jen archivní (100002)
    }

    [Fact]
    public async Task HarvestScopeAsync_All_IncludesBothScopes()
    {
        using var db = InMemoryDb();
        db.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 10, Cislo = "100001" },
            new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 10, Cislo = "100002" }
        );
        await db.SaveChangesAsync();

        var datum = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(q => q.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
          {
              ["100001"] = new HotZaznamFingerprintDto("100001", datum, "otevreno"),
              ["100002"] = new HotZaznamFingerprintDto("100002", datum, "archiv")
          });
        vq.Setup(q => q.GetVyjadreniSecondaryFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Dictionary<string, VyjadreniSecondaryFingerprintDto>());

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestScopeAsync(HarvestScope.All, SyncTriggerKind.Auto, CancellationToken.None);

        result.TicketsChecked.Should().Be(2);
    }
}
