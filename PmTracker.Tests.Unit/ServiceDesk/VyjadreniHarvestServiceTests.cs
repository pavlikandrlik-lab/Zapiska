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

public sealed class VyjadreniHarvestServiceTests
{
    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("harvest-" + Guid.NewGuid())
            .Options);

    private static readonly Guid K3Key = Guid.Parse("11111111-1111-1111-1111-111111111103");
    private static readonly Guid K4Key = Guid.Parse("11111111-1111-1111-1111-111111111104");
    private static readonly Guid K6Key = Guid.Parse("11111111-1111-1111-1111-111111111106");
    private static readonly Guid K7Key = Guid.Parse("11111111-1111-1111-1111-111111111107");
    private static readonly Guid K10Key = Guid.Parse("11111111-1111-1111-1111-111111111110");

    private static async Task SeedSchemaAsync(PmTrackerDbContext db, int sablonaVerze = 1)
    {
        var kroky = new[]
        {
            (poradi: 3, kod: "HS03_DURATION", key: K3Key),
            (poradi: 4, kod: "HS04_DURATION", key: K4Key),
            (poradi: 6, kod: "HS06_DURATION", key: K6Key),
            (poradi: 7, kod: "HS07_DURATION", key: K7Key),
            (poradi: 10, kod: "HS10_DURATION", key: K10Key),
        };
        var nextId = 1;
        foreach (var k in kroky)
        {
            db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
            {
                Id = nextId++,
                Kod = k.kod,
                Nazev = k.kod,
                Hodnota = 10,
                IsLocked = false,
                SablonaVerze = sablonaVerze,
                KrokKey = k.key,
                KrokPoradi = k.poradi,
                JeZpozdeni = false,
                BarvaHex = "#EF4444"
            });
        }
        await db.SaveChangesAsync();
    }

    private static VyjadreniHarvestService BuildSut(PmTrackerDbContext db, IVyjadreniQueryService v)
    {
        return new VyjadreniHarvestService(
            db, v,
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero)),
            new PerExterniOdkazLockRegistry(),
            NullLogger<VyjadreniHarvestService>.Instance);
    }

    [Fact]
    public async Task HarvestTicketAsync_K6Pnf_CreatesActiveBinding()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetVyjadreniForTicketAsync("336865", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(99, "25", "A400P023RVVP",
                    new DateTime(2026, 3, 14, 10, 0, 0), "pm.user",
                    "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.",
                    "FIS", 0)
            });

        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestTicketAsync(1, CancellationToken.None);

        result.Created.Should().Be(1);
        var bindings = await db.VyjadreniVazby.Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();
        bindings.Should().HaveCount(1);
        bindings[0].HotVyjadreniId.Should().Be(99);
        bindings[0].KrokKey.Should().Be(K6Key);
        bindings[0].Source.Should().Be((byte)VazbaSource.Auto);
    }

    [Fact]
    public async Task HarvestTicketAsync_UpdatesLastHarvestedAt()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);
        await sut.HarvestTicketAsync(1, CancellationToken.None);

        var eo = await db.ZaznamExterniOdkazy.FindAsync(1);
        eo!.LastHarvestedAt.Should().Be(new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task HarvestTicketAsync_NoMatchingPredicate_NoBindingCreated()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(50, "05", "P", new DateTime(2026, 1, 1), "u", "nějaký náhodný text", "FIS", 0)
            });

        var sut = BuildSut(db, vq.Object);
        var result = await sut.HarvestTicketAsync(1, CancellationToken.None);

        result.Fetched.Should().Be(1);
        result.Skipped.Should().Be(1);
        (await db.VyjadreniVazby.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HarvestTicketAsync_NoExterniOdkaz_ReturnsEmptyWithMessage()
    {
        await using var db = NewDb();
        var vq = new Mock<IVyjadreniQueryService>();
        var sut = BuildSut(db, vq.Object);

        var result = await sut.HarvestTicketAsync(999, CancellationToken.None);

        result.Message.Should().Contain("neexistuje");
        result.Created.Should().Be(0);
    }

    [Fact]
    public async Task HarvestTicketAsync_DuplicateVyjadreni_DoesNotCreateSecondBinding()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        var dto = new HotVyjadreniDto(99, "25", "A400P023RVVP",
            new DateTime(2026, 3, 14), "u",
            "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.", "FIS", 0);

        vq.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { dto });

        var sut = BuildSut(db, vq.Object);
        await sut.HarvestTicketAsync(1, CancellationToken.None);
        await sut.HarvestTicketAsync(1, CancellationToken.None); // 2nd run — stejná data, nesmí duplikovat

        var bindings = await db.VyjadreniVazby.Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();
        bindings.Should().HaveCount(1);
        bindings[0].HotVyjadreniId.Should().Be(99);
    }

    [Fact]
    public async Task HarvestTicketAsync_ManualBindingExists_AutoHarvestSkips()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await SeedSchemaAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = 100,
            KrokKey = K6Key,
            ExterniOdkazId = 1,
            HotVyjadreniId = 77,
            DatumVyjadreni = new DateTime(2026, 3, 1),
            Source = (byte)VazbaSource.Manual,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 3, 1)
        });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(99, "25", "A400P023RVVP",
                    new DateTime(2026, 3, 14), "u",
                    "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.", "FIS", 0)
            });

        var sut = BuildSut(db, vq.Object);
        var result = await sut.HarvestTicketAsync(1, CancellationToken.None);

        result.Skipped.Should().Be(1);
        var active = await db.VyjadreniVazby.Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();
        active.Should().HaveCount(1);
        active[0].HotVyjadreniId.Should().Be(77); // manual zůstal
        active[0].Source.Should().Be((byte)VazbaSource.Manual);
    }

    [Fact]
    public async Task ReHarvestTicketAsync_SupersedesAutoButKeepsManual()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865", LastHarvestedAt = new DateTime(2026, 4, 1) });
        await SeedSchemaAsync(db);

        db.VyjadreniVazby.AddRange(
            new ZaznamHarmonogramVyjadreniVazbaEntity
            {
                ZaznamId = 100, KrokKey = K3Key, ExterniOdkazId = 1,
                HotVyjadreniId = 11, DatumVyjadreni = new DateTime(2026, 2, 1),
                Source = (byte)VazbaSource.Auto, Stav = (byte)VazbaStav.Active,
                CreatedAt = new DateTime(2026, 2, 1)
            },
            new ZaznamHarmonogramVyjadreniVazbaEntity
            {
                ZaznamId = 100, KrokKey = K6Key, ExterniOdkazId = 1,
                HotVyjadreniId = 22, DatumVyjadreni = new DateTime(2026, 2, 5),
                Source = (byte)VazbaSource.Manual, Stav = (byte)VazbaStav.Active,
                CreatedAt = new DateTime(2026, 2, 5)
            });
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);
        _ = await sut.ReHarvestTicketAsync(1, CancellationToken.None);

        var all = await db.VyjadreniVazby.ToListAsync();
        all.Single(x => x.HotVyjadreniId == 11).Stav.Should().Be((byte)VazbaStav.Deleted);
        all.Single(x => x.HotVyjadreniId == 22).Stav.Should().Be((byte)VazbaStav.Active); // Manual beze změny
    }

    [Fact]
    public async Task HarvestRecordAsync_IteratesAllExterniOdkazy()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "111111" },
            new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 100, Cislo = "222222" },
            new ZaznamExterniOdkazEntity { Id = 3, ZaznamId = 999, Cislo = "999999" }); // jiný záznam
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetVyjadreniForTicketAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);
        await sut.HarvestRecordAsync(100, CancellationToken.None);

        vq.Verify(x => x.GetVyjadreniForTicketAsync("111111", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        vq.Verify(x => x.GetVyjadreniForTicketAsync("222222", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        vq.Verify(x => x.GetVyjadreniForTicketAsync("999999", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Review finding Q-10: K4_K7_DodaniReseni predikát musí mapovat na pořadí 4
    /// pokud HOT_ZAZNAMY.typ_zaznamu = "PMP", ne implicitně na 7. Dříve NormalizeTypZaznamu
    /// vracel "" a všechny tickety padly do K7/PNF.
    /// </summary>
    [Fact]
    public async Task HarvestTicketAsync_WhenPmpTicket_K4Predicate_MapsToPoradi4()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        // HOT_ZAZNAMY typ = PMP → K4_K7 musí jít do K4.
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["336865"] = new HotZaznamFingerprintDto("336865", new DateTime(2026, 3, 14), "otevreno", "PMP")
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("336865", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(99, "25", "A400P023RVVP",
                    new DateTime(2026, 3, 14, 10, 0, 0), "pm.user",
                    "Dodavatel přidal řešení a čeká na akceptaci.",
                    "FIS", 0)
            });

        var sut = BuildSut(db, vq.Object);
        var result = await sut.HarvestTicketAsync(1, CancellationToken.None);

        result.Created.Should().Be(1);
        var bindings = await db.VyjadreniVazby.Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();
        bindings.Should().HaveCount(1);
        bindings[0].KrokKey.Should().Be(K4Key, "PMP tiket + K4_K7 predikát = K4 (pořadí 4)");
    }

    /// <summary>
    /// Kontrastní případ — stejný predikát ale typ_zaznamu = "PNF" → K7.
    /// </summary>
    [Fact]
    public async Task HarvestTicketAsync_WhenPnfTicket_K4K7Predicate_MapsToPoradi7()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336866" });
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["336866"] = new HotZaznamFingerprintDto("336866", new DateTime(2026, 3, 14), "otevreno", "PNF")
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("336866", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(99, "25", "A400P023RVVP",
                    new DateTime(2026, 3, 14, 10, 0, 0), "pm.user",
                    "Dodavatel přidal řešení a čeká na akceptaci.",
                    "FIS", 0)
            });

        var sut = BuildSut(db, vq.Object);
        var result = await sut.HarvestTicketAsync(1, CancellationToken.None);

        result.Created.Should().Be(1);
        var bindings = await db.VyjadreniVazby.Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();
        bindings[0].KrokKey.Should().Be(K7Key, "PNF tiket + K4_K7 predikát = K7 (pořadí 7)");
    }

    [Fact]
    public async Task HarvestTicketAsync_TwoExterniOdkazySameZaznam_DoNotTouchEachOthersBindings()
    {
        // H-2 regression: pre-load vazeb musí být per-ticket (scoped na ExterniOdkazId),
        // ne per-zaznam. Jinak by druhý harvest stejného záznamu viděl vazby prvního
        // v change trackeru a při UpsertBindingInMemory by se pletl.
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "111111" });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 100, Cislo = "222222" });
        await SeedSchemaAsync(db);
        await db.SaveChangesAsync();

        var vq = new Mock<IVyjadreniQueryService>();
        // Ticket 1 → K6 (PNF default)
        vq.Setup(x => x.GetVyjadreniForTicketAsync("111111", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(1001, "25", "A400P023RVVP",
                    new DateTime(2026, 3, 10, 10, 0, 0), "pm.user",
                    "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.",
                    "FIS", 0)
            });
        // Ticket 2 → K6 too (same krokKey, DIFFERENT externiOdkazId)
        vq.Setup(x => x.GetVyjadreniForTicketAsync("222222", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(2002, "25", "A400P023RVVP",
                    new DateTime(2026, 3, 11, 10, 0, 0), "pm.user",
                    "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.",
                    "FIS", 0)
            });

        var sut = BuildSut(db, vq.Object);

        var r1 = await sut.HarvestTicketAsync(1, CancellationToken.None);
        var r2 = await sut.HarvestTicketAsync(2, CancellationToken.None);

        r1.Created.Should().Be(1);
        r2.Created.Should().Be(1);

        // Obě vazby aktivní; druhý harvest NESMÍ supersedovat první, protože patří
        // k jinému externímu odkazu (přestože mají stejný KrokKey/ZaznamId).
        var bindings = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Stav == (byte)VazbaStav.Active)
            .OrderBy(x => x.ExterniOdkazId)
            .ToListAsync();
        bindings.Should().HaveCount(2);
        bindings[0].ExterniOdkazId.Should().Be(1);
        bindings[0].HotVyjadreniId.Should().Be(1001);
        bindings[1].ExterniOdkazId.Should().Be(2);
        bindings[1].HotVyjadreniId.Should().Be(2002);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
