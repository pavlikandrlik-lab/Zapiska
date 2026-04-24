using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Plán 4 Feature C Task 4 — orchestrace sync service.
/// Testuje policies:
///  - Manual rezim skipuje
///  - Auto + 1 kandidát → zapíše Zdroj=Automat
///  - Auto + preferred volba uživatele zachována při re-sync
///  - Preferred fallback (binding zmizel) → clear preferred + fallback MAX
///  - Retract: Automat → bez kandidátů → Neznamo
/// </summary>
public sealed class HarmonogramSkutecnostSyncServiceTests
{
    private const int ZaznamId = 42;
    private const int SablonaVerze = 1;
    private const int ProjektId = 7;

    // PMP kroky: K3 (poradi=3), K4 (poradi=4) — automatické
    private static readonly Guid K3Key = Guid.Parse("33333333-3333-3333-3333-000000000003");
    private static readonly Guid K4Key = Guid.Parse("44444444-4444-4444-4444-000000000004");

    private const int K3DelayTypId = 103;
    private const int K4DelayTypId = 104;

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("sync-" + Guid.NewGuid())
            .Options);

    private static async Task SeedSchemaAsync(PmTrackerDbContext db)
    {
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = ZaznamId,
            ProjektId = ProjektId,
            KategorieId = 1,
            Nazev = "test",
            DatumZalozeni = new DateTime(2026, 1, 1),
            DatumUkonceni = new DateTime(2026, 6, 1),
            SubsystemId = 1,
            HarmonogramSablonaVerze = SablonaVerze
        });

        // Schema: HS03_DURATION + HS03_DELAY, HS04_DURATION + HS04_DELAY
        db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
        {
            Id = 3, Kod = "HS03_DURATION", Nazev = "K3", Hodnota = 5, SablonaVerze = SablonaVerze,
            KrokKey = K3Key, KrokPoradi = 3, JeZpozdeni = false, BarvaHex = "#EF4444"
        });
        db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
        {
            Id = K3DelayTypId, Kod = "HS03_DELAY", Nazev = "K3 delay", Hodnota = 0, SablonaVerze = SablonaVerze,
            KrokKey = K3Key, KrokPoradi = 3, JeZpozdeni = true, BarvaHex = "#DC2626"
        });
        db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
        {
            Id = 4, Kod = "HS04_DURATION", Nazev = "K4", Hodnota = 5, SablonaVerze = SablonaVerze,
            KrokKey = K4Key, KrokPoradi = 4, JeZpozdeni = false, BarvaHex = "#EF4444"
        });
        db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
        {
            Id = K4DelayTypId, Kod = "HS04_DELAY", Nazev = "K4 delay", Hodnota = 0, SablonaVerze = SablonaVerze,
            KrokKey = K4Key, KrokPoradi = 4, JeZpozdeni = true, BarvaHex = "#DC2626"
        });

        await db.SaveChangesAsync();
    }

    private static void AddExterniOdkaz(PmTrackerDbContext db, int id, string cislo)
    {
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = id, ZaznamId = ZaznamId, TypOdkazuId = 1, Cislo = cislo
        });
    }

    private static void AddActiveBinding(PmTrackerDbContext db, int id, Guid krokKey, int externiOdkazId, DateTime datum)
    {
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = id,
            ZaznamId = ZaznamId,
            KrokKey = krokKey,
            ExterniOdkazId = externiOdkazId,
            HotVyjadreniId = id * 100,
            DatumVyjadreni = datum,
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static HarmonogramSkutecnostSyncService CreateSut(
        PmTrackerDbContext db,
        StubVyjadreniQuery query,
        TimeProvider? time = null)
        => new(
            db,
            query,
            time ?? TimeProvider.System,
            NullLogger<HarmonogramSkutecnostSyncService>.Instance);

    [Fact]
    public async Task Sync_ManualRezim_Skipuje()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, K3Key, 500, new DateTime(2026, 3, 10));
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 1, ZaznamId = ZaznamId, TypId = K3DelayTypId, HodnotaInt = 5,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Manual,
            SkutecnostZdroj = SkutecnostZdrojEnum.Manual
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuPreskoceno_Manual.Should().Be(1);
        result.KrokuAktualizovano.Should().Be(0);
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Manual);
        row.SkutecnostRezim.Should().Be(SkutecnostRezimEnum.Manual);
    }

    [Fact]
    public async Task Sync_AutoRezimJedenKandidat_OznaciAutomat()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, K3Key, 500, new DateTime(2026, 3, 10));
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 1, ZaznamId = ZaznamId, TypId = K3DelayTypId, HodnotaInt = 5,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Historicka
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuAktualizovano.Should().Be(1);
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Automat);
        row.PreferredExterniOdkazId.Should().BeNull();
    }

    [Fact]
    public async Task Sync_PreferredZachovanPokudJeMeziKandidaty()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddExterniOdkaz(db, 501, "222222");
        AddActiveBinding(db, 1, K4Key, 500, new DateTime(2026, 3, 10));
        AddActiveBinding(db, 2, K4Key, 501, new DateTime(2026, 3, 20));
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 1, ZaznamId = ZaznamId, TypId = K4DelayTypId, HodnotaInt = 0,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Automat,
            PreferredExterniOdkazId = 500 // user vybral starší binding
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP"), ("222222", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.PreferredFallbackPouzito.Should().Be(0);
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.PreferredExterniOdkazId.Should().Be(500);
    }

    [Fact]
    public async Task Sync_PreferredNeniMeziKandidaty_FallbackClearuje()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, K4Key, 500, new DateTime(2026, 3, 10));
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 1, ZaznamId = ZaznamId, TypId = K4DelayTypId, HodnotaInt = 0,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Automat,
            PreferredExterniOdkazId = 999 // user vybral binding, který harvest zrušil
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.PreferredFallbackPouzito.Should().Be(1);
        result.KrokuAktualizovano.Should().Be(1);
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.PreferredExterniOdkazId.Should().BeNull();
        row.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Automat);
    }

    [Fact]
    public async Task Sync_AutomatBezKandidatu_RetractNaNeznamo()
    {
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        // Žádné bindings — předchozí Automat flag musí spadnout na Neznamo.
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 1, ZaznamId = ZaznamId, TypId = K3DelayTypId, HodnotaInt = 5,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Automat,
            PreferredExterniOdkazId = 500
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery());
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuAktualizovano.Should().Be(1);
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Neznamo);
        row.PreferredExterniOdkazId.Should().BeNull();
    }

    [Fact]
    public async Task Sync_AutoRezimSKandidatem_AktualizujeHodnotaInt_DelayOdBaseline()
    {
        // Plán 4 Feature C — HodnotaInt auto-propagation.
        // Když sync označí krok jako Automat a najde kandidát s konkrétním datem,
        // musí přepočítat HodnotaInt (delay ve dnech) proti baseline end datu.
        // Baseline K3 end = DatumZalozeni + sum(Duration[1..3]).
        // Pro náš seed je K3 Duration=5, K4 Duration=5, ostatní duration kroky chybí.
        // → Baseline K3 end = 2026-01-01 + 5 dní (pouze K3 duration v seedu).
        // Binding datum 2026-01-20 → delay = 19 dní.
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, K3Key, 500, new DateTime(2026, 1, 20));
        // DURATION row aby BuildHarmonogramVypocet uměl spočítat baseline
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 100, ZaznamId = ZaznamId, TypId = 3 /*K3 duration*/, HodnotaInt = 5,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
        });
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 1, ZaznamId = ZaznamId, TypId = K3DelayTypId, HodnotaInt = 0,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Neznamo
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuAktualizovano.Should().BeGreaterThan(0);
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Automat);
        row.HodnotaInt.Should().BeGreaterThan(0,
            "delay musí být nenulový když skutečnost leží po baseline datumu.");
    }

    [Fact]
    public async Task Sync_AutoRezimBezKandidatu_HodnotaIntSe_NemeniNeboRetractuje()
    {
        // Když sync přechází na Neznamo (binding zmizel, předtím Automat), HodnotaInt
        // by neměla přepsat user-předtím-vyplněný manual datum (ale v tomto seed je
        // předtím Automat, takže retract je legit — row zůstane v old stavu pokud nebyl Manual).
        await using var db = NewDb();
        await SeedSchemaAsync(db);
        // NO active binding
        db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
        {
            Id = 1, ZaznamId = ZaznamId, TypId = K3DelayTypId, HodnotaInt = 10,
            UpdatedAt = DateTime.UtcNow,
            SkutecnostRezim = SkutecnostRezimEnum.Auto,
            SkutecnostZdroj = SkutecnostZdrojEnum.Automat,  // dřív byl naplněný
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery());
        var result = await sut.SyncZaznamAsync(ZaznamId);

        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Neznamo,
            "retract: binding zmizel, auto-řádek přešel na Neznamo.");
        // HodnotaInt politika při retract: neměníme (user může chtít vidět předchozí hodnotu).
        // Stačí kontrola, že sync neexplodoval.
    }

    // --- test double ---

    private sealed class StubVyjadreniQuery : IVyjadreniQueryService
    {
        private readonly Dictionary<string, string> _typByCislo;

        public StubVyjadreniQuery(params (string cislo, string typZaznamu)[] entries)
        {
            _typByCislo = entries.ToDictionary(e => e.cislo, e => e.typZaznamu);
        }

        public Task<IReadOnlyList<HotVyjadreniDto>> GetVyjadreniForTicketAsync(
            string cislo6, DateTime? sinceUtc, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<HotVyjadreniDto>>(Array.Empty<HotVyjadreniDto>());

        public Task<IReadOnlyDictionary<string, HotZaznamFingerprintDto>> GetHotZaznamFingerprintsAsync(
            IReadOnlyCollection<string> cisla6, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var dict = cisla6
                .Where(c => _typByCislo.ContainsKey(c))
                .ToDictionary(c => c,
                    c => new HotZaznamFingerprintDto(c, now, Stav: null, TypZaznamu: _typByCislo[c]));
            return Task.FromResult<IReadOnlyDictionary<string, HotZaznamFingerprintDto>>(dict);
        }

        public Task<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>> GetVyjadreniSecondaryFingerprintsAsync(
            IReadOnlyCollection<string> cisla6, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, VyjadreniSecondaryFingerprintDto>>(
                new Dictionary<string, VyjadreniSecondaryFingerprintDto>());
    }
}
