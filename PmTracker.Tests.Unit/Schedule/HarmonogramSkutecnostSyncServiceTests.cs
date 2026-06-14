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
/// Datum-model (2026-06-12) — orchestrace sync service nad <c>zaznam_harmonogram_krok</c>.
/// Testuje policies:
///  - Manual rezim skipuje
///  - Auto + 1 kandidát → zapíše SkutecnostDatum + Zdroj=Automat
///  - Auto + preferred volba uživatele zachována při re-sync
///  - Preferred fallback (binding zmizel) → clear preferred + fallback MAX
///  - Retract: Automat → bez kandidátů → Neznamo
///
/// Kroky jsou pevné (HarmonogramKroky.Vse), bez číselníku schématu. K3/K4 (poradi 3/4) jsou
/// PMP auto-fill kroky. Vazba nese pořadí (Poradi).
/// </summary>
public sealed class HarmonogramSkutecnostSyncServiceTests
{
    private const int ZaznamId = 42;
    private const int ProjektId = 7;

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("sync-" + Guid.NewGuid())
            .Options);

    private static async Task SeedRecordAsync(PmTrackerDbContext db)
    {
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = ZaznamId,
            ProjektId = ProjektId,
            KategorieId = 1,
            Nazev = "test",
            DatumZalozeni = new DateTime(2026, 1, 1),
            DatumUkonceni = new DateTime(2026, 6, 1),
            SubsystemId = 1
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

    private static void AddActiveBinding(PmTrackerDbContext db, int id, int poradi, int externiOdkazId, DateTime datum)
    {
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = id,
            ZaznamId = ZaznamId,
            Poradi = (byte)poradi,
            ExterniOdkazId = externiOdkazId,
            HotVyjadreniId = id * 100,
            DatumVyjadreni = datum,
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static void AddKrokRow(
        PmTrackerDbContext db, int id, int poradi,
        SkutecnostRezimEnum rezim, SkutecnostZdrojEnum zdroj,
        DateTime? skutecnostDatum = null, int? preferredExterniOdkazId = null)
    {
        db.ZaznamHarmonogramKroky.Add(new ZaznamHarmonogramKrokEntity
        {
            Id = id,
            ZaznamId = ZaznamId,
            Poradi = (byte)poradi,
            SkutecnostDatum = skutecnostDatum,
            SkutecnostRezim = (byte)rezim,
            SkutecnostZdroj = (byte)zdroj,
            PreferredExterniOdkazId = preferredExterniOdkazId,
            UpdatedAt = DateTime.UtcNow
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
        await SeedRecordAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, poradi: 3, 500, new DateTime(2026, 3, 10));
        AddKrokRow(db, 1, poradi: 3, SkutecnostRezimEnum.Manual, SkutecnostZdrojEnum.Manual,
            skutecnostDatum: new DateTime(2026, 2, 2));
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuPreskoceno_Manual.Should().Be(1);
        result.KrokuAktualizovano.Should().Be(0);
        var row = await db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Manual);
        row.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Manual);
        row.SkutecnostDatum.Should().Be(new DateTime(2026, 2, 2));
    }

    [Fact]
    public async Task Sync_AutoRezimJedenKandidat_OznaciAutomat()
    {
        await using var db = NewDb();
        await SeedRecordAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, poradi: 3, 500, new DateTime(2026, 3, 10));
        AddKrokRow(db, 1, poradi: 3, SkutecnostRezimEnum.Auto, SkutecnostZdrojEnum.Historicka);
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuAktualizovano.Should().Be(1);
        var row = await db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
        row.SkutecnostDatum.Should().Be(new DateTime(2026, 3, 10));
        row.PreferredExterniOdkazId.Should().BeNull();
    }

    /// <summary>
    /// Fresh harvest scénář — krok row pro krok ještě neexistuje. Sync má kandidáta z bindings,
    /// musí vytvořit krok row jako Automat + SkutecnostDatum.
    /// </summary>
    [Fact]
    public async Task Sync_AutoRezim_KrokRowNeexistuje_CreateAutomatRow_VytvoriRow()
    {
        await using var db = NewDb();
        await SeedRecordAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, poradi: 3, 500, new DateTime(2026, 3, 10));
        // ZÁMĚRNĚ NEPŘIDÁVÁME krok row pro K3 — fresh harvest scenario.
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuAktualizovano.Should().Be(1, "sync má vytvořit chybějící krok row");
        var row = await db.ZaznamHarmonogramKroky.AsNoTracking()
            .SingleAsync(h => h.ZaznamId == ZaznamId && h.Poradi == 3);
        row.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
        row.SkutecnostRezim.Should().Be((byte)SkutecnostRezimEnum.Auto);
        row.SkutecnostDatum.Should().Be(new DateTime(2026, 3, 10));
        row.PreferredExterniOdkazId.Should().BeNull(
            "preferred je null protože sync neměl explicitní výběr kandidáta od usera");
    }

    /// <summary>
    /// Negativní case: fresh záznam bez kandidáta (žádné bindings) → sync NESMÍ vytvořit
    /// prázdné krok rows "do zásoby". No-op pro krok bez kandidáta.
    /// </summary>
    [Fact]
    public async Task Sync_AutoRezim_KrokRowNeexistuje_BezKandidatu_NicNevytvori()
    {
        await using var db = NewDb();
        await SeedRecordAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        // ŽÁDNÝ AddActiveBinding — záznam má externí odkaz ale žádné aktivní vazby na kroky.
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuAktualizovano.Should().Be(0, "bez kandidáta sync nesmí vytvořit krok row");
        var rows = await db.ZaznamHarmonogramKroky.AsNoTracking()
            .Where(h => h.ZaznamId == ZaznamId).ToListAsync();
        rows.Should().BeEmpty("žádné krok rows se nevytvoří 'do zásoby'");
    }

    [Fact]
    public async Task Sync_PreferredZachovanPokudJeMeziKandidaty()
    {
        await using var db = NewDb();
        await SeedRecordAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddExterniOdkaz(db, 501, "222222");
        AddActiveBinding(db, 1, poradi: 4, 500, new DateTime(2026, 3, 10));
        AddActiveBinding(db, 2, poradi: 4, 501, new DateTime(2026, 3, 20));
        AddKrokRow(db, 1, poradi: 4, SkutecnostRezimEnum.Auto, SkutecnostZdrojEnum.Automat,
            skutecnostDatum: new DateTime(2026, 3, 10), preferredExterniOdkazId: 500);
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP"), ("222222", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.PreferredFallbackPouzito.Should().Be(0);
        var row = await db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.PreferredExterniOdkazId.Should().Be(500);
        row.SkutecnostDatum.Should().Be(new DateTime(2026, 3, 10), "preferred binding 500 má datum 3/10");
    }

    [Fact]
    public async Task Sync_PreferredNeniMeziKandidaty_FallbackClearuje()
    {
        await using var db = NewDb();
        await SeedRecordAsync(db);
        AddExterniOdkaz(db, 500, "111111");
        AddActiveBinding(db, 1, poradi: 4, 500, new DateTime(2026, 3, 10));
        AddKrokRow(db, 1, poradi: 4, SkutecnostRezimEnum.Auto, SkutecnostZdrojEnum.Automat,
            skutecnostDatum: new DateTime(2026, 1, 1), preferredExterniOdkazId: 999);
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery(("111111", "PMP")));
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.PreferredFallbackPouzito.Should().Be(1);
        result.KrokuAktualizovano.Should().Be(1);
        var row = await db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.PreferredExterniOdkazId.Should().BeNull();
        row.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Automat);
        row.SkutecnostDatum.Should().Be(new DateTime(2026, 3, 10), "fallback MAX = jediný binding 500");
    }

    [Fact]
    public async Task Sync_AutomatBezKandidatu_RetractNaNeznamo()
    {
        await using var db = NewDb();
        await SeedRecordAsync(db);
        // Žádné bindings — předchozí Automat flag musí spadnout na Neznamo.
        AddKrokRow(db, 1, poradi: 3, SkutecnostRezimEnum.Auto, SkutecnostZdrojEnum.Automat,
            skutecnostDatum: new DateTime(2026, 3, 5), preferredExterniOdkazId: 500);
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new StubVyjadreniQuery());
        var result = await sut.SyncZaznamAsync(ZaznamId);

        result.KrokuAktualizovano.Should().Be(1);
        var row = await db.ZaznamHarmonogramKroky.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostZdroj.Should().Be((byte)SkutecnostZdrojEnum.Neznamo);
        row.SkutecnostDatum.Should().BeNull("retract vyčistí auto-skutečnost");
        row.PreferredExterniOdkazId.Should().BeNull();
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
