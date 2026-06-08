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
    /// FIX 2026-05-04 regression: real intranetNEW může mít sloupec typ_zaznamu jako CHAR(5)
    /// s trailing whitespace ("PMP  "). Před fixem `string.Equals(raw, "PMP", OrdinalIgnoreCase)`
    /// v MapKindToPoradi vrátil false a PMP K4_K7 vyjádření se ukládalo s KrokPoradi=7
    /// (PNF behavior). Resolver pak pro krok 4 nenašel kandidáta → po přidání PNF tiketu
    /// PNF datum „přebilo" PMP datum. Fix: NormalizeTypZaznamu(Trim+ToUpperInvariant) v
    /// HarvestTicketCoreAsync. Test ověří, že padded "PMP  " stále mapuje K4_K7 → K4.
    /// </summary>
    [Fact]
    public async Task HarvestTicketAsync_WhenPmpTicketWithPaddedTypZaznamu_MapsToPoradi4()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity { Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1 });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "336865" });
        await SeedSchemaAsync(db);

        var vq = new Mock<IVyjadreniQueryService>();
        // Simulujeme CHAR(5) padding — TypZaznamu = "PMP  " (s 2 trailing spaces).
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["336865"] = new HotZaznamFingerprintDto("336865", new DateTime(2026, 3, 14), "otevreno", "PMP  ")
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
        bindings[0].KrokKey.Should().Be(K4Key,
            "padded \"PMP  \" musí po normalizaci stále mapovat K4_K7 → K4 (ne fallback K7)");
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

    // ---------- Spec 2026-04-28: NES skip stepper + synthetic K1 ----------

    private static readonly Guid K1Key = Guid.Parse("11111111-1111-1111-1111-111111111101");

    private static async Task SeedSchemaWithK1Async(PmTrackerDbContext db, int sablonaVerze = 1)
    {
        var kroky = new[]
        {
            (poradi: 1, kod: "HS01_DURATION", key: K1Key),
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

    [Fact]
    public async Task HarvestTicketAsync_NesTicket_NevytvariStepperBindings()
    {
        // Spec 2026-04-28 §1: NES je úplně odpojen od harmonogramu — žádné stepper bindings.
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "100001" });
        await SeedSchemaWithK1Async(db);

        var vq = new Mock<IVyjadreniQueryService>();
        // Fingerprint vrátí typ NES.
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["100001"] = new HotZaznamFingerprintDto("100001", new DateTime(2026, 1, 1),
                    Stav: "novy", TypZaznamu: "NES", SlaDeadline: null),
            });
        // I když NES popis obsahuje K10 frázi, žádné binding by se NEMĚL vytvořit.
        vq.Setup(x => x.GetVyjadreniForTicketAsync("100001", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new HotVyjadreniDto(99, "25", "PID-NES-1",
                    new DateTime(2026, 3, 14, 10, 0, 0), "uzivatel",
                    "Záznam byl převeden do archivu.", "Tym", 1)
            });

        var sut = BuildSut(db, vq.Object);
        var result = await sut.HarvestTicketAsync(1, CancellationToken.None);

        result.Created.Should().Be(0);
        var bindings = await db.VyjadreniVazby.ToListAsync();
        bindings.Should().BeEmpty();
    }

    [Fact]
    public async Task HarvestTicketAsync_PmpTicket_VytvoriSyntheticK1Binding()
    {
        // Spec 2026-04-28 §1: PMP/PNF dostávají synthetic K1 binding z HOT_ZAZNAMY.datum.
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "200001" });
        await SeedSchemaWithK1Async(db);

        var hotZaznamDatum = new DateTime(2026, 1, 15, 9, 30, 0);
        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["200001"] = new HotZaznamFingerprintDto("200001", hotZaznamDatum,
                    Stav: "novy", TypZaznamu: "PMP", SlaDeadline: null),
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("200001", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);
        await sut.HarvestTicketAsync(1, CancellationToken.None);

        var k1Bindings = await db.VyjadreniVazby
            .Where(x => x.KrokKey == K1Key && x.Stav == (byte)VazbaStav.Active)
            .ToListAsync();
        k1Bindings.Should().HaveCount(1);
        k1Bindings[0].HotVyjadreniId.Should().Be(0L);  // synthetic
        k1Bindings[0].DatumVyjadreni.Should().Be(hotZaznamDatum);
        k1Bindings[0].Source.Should().Be((byte)VazbaSource.Auto);
        k1Bindings[0].ExterniOdkazId.Should().Be(1);
    }

    [Fact]
    public async Task HarvestTicketAsync_PnfTicket_VytvoriSyntheticK1Binding()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "300001" });
        await SeedSchemaWithK1Async(db);

        var hotZaznamDatum = new DateTime(2026, 2, 1);
        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["300001"] = new HotZaznamFingerprintDto("300001", hotZaznamDatum,
                    Stav: "novy", TypZaznamu: "PNF", SlaDeadline: null),
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("300001", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);
        await sut.HarvestTicketAsync(1, CancellationToken.None);

        var k1Bindings = await db.VyjadreniVazby
            .Where(x => x.KrokKey == K1Key && x.Stav == (byte)VazbaStav.Active)
            .ToListAsync();
        k1Bindings.Should().HaveCount(1);
        k1Bindings[0].HotVyjadreniId.Should().Be(0L);
        k1Bindings[0].DatumVyjadreni.Should().Be(hotZaznamDatum);
    }

    [Fact]
    public async Task HarvestTicketAsync_PmpReHarvestSeStejnymDatem_NeduplikujeK1()
    {
        // 2× harvest se stejným HOT_ZAZNAMY.datum → druhý vrací Skipped (deduplikace).
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "400001" });
        await SeedSchemaWithK1Async(db);

        var hotZaznamDatum = new DateTime(2026, 1, 15);
        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["400001"] = new HotZaznamFingerprintDto("400001", hotZaznamDatum,
                    Stav: "novy", TypZaznamu: "PMP", SlaDeadline: null),
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("400001", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);
        await sut.HarvestTicketAsync(1, CancellationToken.None);
        await sut.HarvestTicketAsync(1, CancellationToken.None);

        var k1All = await db.VyjadreniVazby
            .Where(x => x.KrokKey == K1Key)
            .ToListAsync();
        k1All.Should().HaveCount(1);  // jen jeden binding (deduplikace)
        k1All[0].Stav.Should().Be((byte)VazbaStav.Active);
    }

    [Fact]
    public async Task HarvestTicketAsync_NeznamyTyp_NevytvariK1Binding()
    {
        // Pokud typ není PMP/PNF/NES (např. RU), žádný K1 binding (NES skip stejně, PMP/PNF
        // vyžaduje, aby krokKeyByPoradi[1] existoval — pro neznámé typy by se mohlo stát,
        // že schema verze nemá K1, ale to je už chráněno TryGetValue).
        // Tento test ověří NES branch: typ NES → žádný K1.
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 100, ProjektId = 1, Nazev = "Z", HarmonogramSablonaVerze = 1
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 100, Cislo = "500001" });
        await SeedSchemaWithK1Async(db);

        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetHotZaznamFingerprintsAsync(
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, HotZaznamFingerprintDto>
            {
                ["500001"] = new HotZaznamFingerprintDto("500001", new DateTime(2026, 1, 15),
                    Stav: "novy", TypZaznamu: "NES", SlaDeadline: null),
            });
        vq.Setup(x => x.GetVyjadreniForTicketAsync("500001", It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<HotVyjadreniDto>());

        var sut = BuildSut(db, vq.Object);
        await sut.HarvestTicketAsync(1, CancellationToken.None);

        var allBindings = await db.VyjadreniVazby.ToListAsync();
        allBindings.Should().BeEmpty();   // NES = žádné bindings (ani K1)
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
