using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Integration-styled testy s in-memory EF + mock IVyjadreniQueryService.
/// Pokrývá happy-path zápisu 4 datumů, no-op když data už souhlasí, přepis ručních
/// hodnot (auto-fill je default), a kontrolu, že chybějící tiket nezpůsobí výjimku.
/// </summary>
public sealed class PerTicketMetadataSyncServiceTests
{
    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("metasync-" + Guid.NewGuid())
            .Options);

    private static PerTicketMetadataSyncService BuildSut(PmTrackerDbContext db, IVyjadreniQueryService v)
        => new(db, v, NullLogger<PerTicketMetadataSyncService>.Instance);

    private static HotVyjadreniDto V(long id, string datum, string popis)
        => new(id, "25", "PID-1", DateTime.Parse(datum), "uzivatel", popis, "Tym", 1);

    [Fact]
    public async Task SyncTicketAsync_NesTicket_ZapisePlanDodaniZeSlaDeadline()
    {
        await using var db = NewDb();
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 1, ZaznamId = 100, Cislo = "100001",
        });
        await db.SaveChangesAsync();

        var sla = new DateTime(2026, 4, 30, 12, 0, 0);
        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.Is<IReadOnlyCollection<string>>(c => c.Contains("100001")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["100001"] = new HotZaznamFingerprintDto("100001", DateTime.Parse("2026-01-01"),
                    Stav: "novy", TypZaznamu: "NES", SlaDeadline: sla),
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("100001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                V(1, "2026-01-01", "Záznam byl předán dodavateli k řešení."),
                V(2, "2026-02-15", "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 123456 vytvořena."),
            });

        var sut = BuildSut(db, vq.Object);
        await sut.SyncTicketAsync(1, CancellationToken.None);

        var refreshed = await db.ZaznamExterniOdkazy.FirstAsync(x => x.Id == 1);
        refreshed.PlanDodani.Should().Be(new DateTime(2026, 4, 30));
        refreshed.DatumObjednani.Should().Be(new DateTime(2026, 1, 1));
        refreshed.DatumDodani.Should().Be(new DateTime(2026, 2, 15));
        refreshed.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public async Task SyncTicketAsync_PmpTicket_ZapiseDatumyZTextu()
    {
        await using var db = NewDb();
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 5, ZaznamId = 100, Cislo = "200001",
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["200001"] = new HotZaznamFingerprintDto("200001", DateTime.Parse("2026-01-01"),
                    Stav: "novy", TypZaznamu: "PMP", SlaDeadline: null),
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("200001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                V(1, "2026-01-01", "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
                V(2, "2026-01-02", "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
                V(3, "2026-04-10", "Dodavatel přidal řešení."),
            });

        var sut = BuildSut(db, vq.Object);
        await sut.SyncTicketAsync(5, CancellationToken.None);

        var refreshed = await db.ZaznamExterniOdkazy.FirstAsync(x => x.Id == 5);
        refreshed.DatumObjednani.Should().Be(new DateTime(2026, 1, 2));
        refreshed.PlanDodani.Should().Be(new DateTime(2026, 4, 15));
        refreshed.DatumDodani.Should().Be(new DateTime(2026, 4, 10));
        refreshed.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public async Task SyncTicketAsync_ExistujiciRucniDatumy_AutoFillPrepise()
    {
        await using var db = NewDb();
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 7, ZaznamId = 100, Cislo = "300001",
            DatumObjednani = new DateTime(2025, 12, 31),  // pre-existing manual
            PlanDodani = new DateTime(2025, 12, 31),
            DatumDodani = new DateTime(2025, 12, 31),
            DatumPrevzeti = new DateTime(2025, 12, 31),
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["300001"] = new HotZaznamFingerprintDto("300001", DateTime.Parse("2026-01-01"),
                    Stav: "novy", TypZaznamu: "PNF", SlaDeadline: null),
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("300001", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                V(1, "2026-02-15", "Dodavatel přidal řešení."),
            });

        var sut = BuildSut(db, vq.Object);
        await sut.SyncTicketAsync(7, CancellationToken.None);

        var refreshed = await db.ZaznamExterniOdkazy.FirstAsync(x => x.Id == 7);
        // Pre-existing 2025-12-31 hodnoty se přepsaly. Auto-fill najde jen DatumDodani,
        // ostatní jsou null (přepsalo se z 2025-12-31 na null).
        refreshed.DatumObjednani.Should().BeNull();
        refreshed.PlanDodani.Should().BeNull();
        refreshed.DatumDodani.Should().Be(new DateTime(2026, 2, 15));
        refreshed.DatumPrevzeti.Should().BeNull();
    }

    [Fact]
    public async Task SyncTicketAsync_TiketNeexistujeVHotZaznamy_NicNezapise()
    {
        await using var db = NewDb();
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 11, ZaznamId = 100, Cislo = "999999",
            DatumObjednani = new DateTime(2025, 1, 1),
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>());

        var sut = BuildSut(db, vq.Object);
        await sut.SyncTicketAsync(11, CancellationToken.None);

        var refreshed = await db.ZaznamExterniOdkazy.FirstAsync(x => x.Id == 11);
        // Nezměněno — fingerprint nebyl nalezen.
        refreshed.DatumObjednani.Should().Be(new DateTime(2025, 1, 1));
    }

    [Fact]
    public async Task SyncTicketAsync_NeexistujiciExterniOdkaz_NicNeudela()
    {
        await using var db = NewDb();
        var vq = new Mock<IVyjadreniQueryService>();

        var sut = BuildSut(db, vq.Object);
        var act = async () => await sut.SyncTicketAsync(externiOdkazId: 9999, CancellationToken.None);

        await act.Should().NotThrowAsync();
        vq.Verify(x => x.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncTicketAsync_PrazdneCislo_NicNeudela()
    {
        await using var db = NewDb();
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 13, ZaznamId = 100, Cislo = "",
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        var sut = BuildSut(db, vq.Object);

        await sut.SyncTicketAsync(13, CancellationToken.None);

        vq.Verify(x => x.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
