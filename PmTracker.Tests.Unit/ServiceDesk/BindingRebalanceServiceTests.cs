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
/// Unit testy pro <see cref="BindingRebalanceService"/> — service, který wrapuje
/// <see cref="ChronologyRebalancer"/> a aplikuje výsledky do DB pod jednou transakcí
/// a per-externiOdkazId semaforem. Volaný z <c>VyjadreniModalController.CreateVazba</c>
/// při manual drag-and-drop.
///
/// Datum-model (2026-06-12): krok je identifikovaný pořadím 1–10 (Poradi), ne Guid KrokKey.
/// </summary>
public sealed class BindingRebalanceServiceTests
{
    private const int ZaznamId = 500;
    private const int ExterniOdkazId = 9001;
    private const string TicketCislo = "123456";

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("rebalance-" + Guid.NewGuid())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task SeedBaseAsync(PmTrackerDbContext db)
    {
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = ZaznamId,
            ProjektId = 1,
            Nazev = "Z",
            SubsystemId = 1,
            KategorieId = 1
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = ExterniOdkazId,
            ZaznamId = ZaznamId,
            Cislo = TicketCislo
        });
        await db.SaveChangesAsync();
    }

    private static BindingRebalanceService BuildSut(
        PmTrackerDbContext db,
        IVyjadreniQueryService vq,
        DateTime? nowUtc = null)
    {
        var time = new FakeTimeProvider(
            new DateTimeOffset(nowUtc ?? new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));
        return new BindingRebalanceService(db, vq, time,
            new PerExterniOdkazLockRegistry(),
            NullLogger<BindingRebalanceService>.Instance);
    }

    private static Mock<IVyjadreniQueryService> MockAvailableBubbles(params (long id, DateTime datum)[] bubbles)
    {
        var vq = new Mock<IVyjadreniQueryService>();
        vq.Setup(x => x.GetVyjadreniForTicketAsync(TicketCislo, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bubbles.Select(b => new HotVyjadreniDto(
                b.id, "25", "PID", b.datum, "user", "popis", "team", 0)).ToList());
        return vq;
    }

    private static BindingRebalanceRequest RequestFor(
        int krokPoradi, long hotVyjadreniId, DateTime datum, int osobaId = 777)
    {
        return new BindingRebalanceRequest(
            ZaznamId: ZaznamId,
            ExterniOdkazId: ExterniOdkazId,
            KrokPoradi: krokPoradi,
            HotVyjadreniId: hotVyjadreniId,
            DatumVyjadreni: datum,
            OsobaId: osobaId);
    }

    [Fact]
    public async Task CreateBindingAsync_WhenNoExistingBindings_CreatesOnlyTargetBinding()
    {
        await using var db = NewDb();
        await SeedBaseAsync(db);
        var vq = MockAvailableBubbles();
        var sut = BuildSut(db, vq.Object);

        var result = await sut.CreateBindingAsync(
            RequestFor(6, hotVyjadreniId: 101L, datum: new DateTime(2026, 3, 1)),
            CancellationToken.None);

        result.PrimaryVazbaId.Should().BePositive();
        result.CascadeUpdates.Should().BeEmpty();

        var bindings = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();
        bindings.Should().HaveCount(1);
        bindings[0].Poradi.Should().Be(6);
        bindings[0].HotVyjadreniId.Should().Be(101L);
        bindings[0].Source.Should().Be((byte)VazbaSource.Manual);
    }

    [Fact]
    public async Task CreateBindingAsync_WhenLaterStepHasEarlierBubble_ReplacesCascadeBubble()
    {
        // K6 na bublinu #100 s datem 3/1. User pak dá K3 bublinu #200 s datem 4/1 (pozdější než K6).
        // To rozbíjí chronologii: K6 by měla být > K3. Rebalancer má:
        //   - pokud K6 má bublinu < K3.datum → najít kandidáta > K3.datum
        //   - pokud není kandidát → K6 do bufferu (null)
        // Scenario: K3 se nastavuje DATEM 5/1, existující K6=100@3/1, dostupná bublina 200@6/1.
        await using var db = NewDb();
        await SeedBaseAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = ZaznamId,
            ExterniOdkazId = ExterniOdkazId,
            Poradi = 6,
            HotVyjadreniId = 100L,
            DatumVyjadreni = new DateTime(2026, 3, 1),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 1)
        });
        await db.SaveChangesAsync();

        var vq = MockAvailableBubbles(
            (100L, new DateTime(2026, 3, 1)),
            (200L, new DateTime(2026, 6, 1)));
        var sut = BuildSut(db, vq.Object);

        // Target: K3 (poradi 3) s datem 5/1, bublina 300.
        var result = await sut.CreateBindingAsync(
            RequestFor(3, hotVyjadreniId: 300L, datum: new DateTime(2026, 5, 1)),
            CancellationToken.None);

        result.PrimaryVazbaId.Should().BePositive();
        result.CascadeUpdates.Should().NotBeEmpty();

        var activeBindings = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Stav == (byte)VazbaStav.Active)
            .OrderBy(x => x.Poradi).ToListAsync();

        activeBindings.Should().Contain(b => b.Poradi == 3 && b.HotVyjadreniId == 300L && b.Source == (byte)VazbaSource.Manual);
        activeBindings.Should().Contain(b => b.Poradi == 6 && b.HotVyjadreniId == 200L && b.Source == (byte)VazbaSource.ChronologyCascade);

        // Původní K6=100 musí být superseded, ne active.
        var supersededForK6 = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Poradi == 6 && x.Stav == (byte)VazbaStav.Superseded)
            .ToListAsync();
        supersededForK6.Should().ContainSingle(b => b.HotVyjadreniId == 100L);
    }

    [Fact]
    public async Task CreateBindingAsync_WhenLaterStepHasNoCandidate_LeavesLaterStepEmpty()
    {
        // K6=100@3/1 existuje. Target K3 = datum 5/1 (později než K6). Dostupná bublina:
        // jen ta stávající 100@3/1 (která je too old). Žádný kandidát > 5/1 → K6 do bufferu.
        await using var db = NewDb();
        await SeedBaseAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = ZaznamId,
            ExterniOdkazId = ExterniOdkazId,
            Poradi = 6,
            HotVyjadreniId = 100L,
            DatumVyjadreni = new DateTime(2026, 3, 1),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 1)
        });
        await db.SaveChangesAsync();

        var vq = MockAvailableBubbles((100L, new DateTime(2026, 3, 1)));
        var sut = BuildSut(db, vq.Object);

        var result = await sut.CreateBindingAsync(
            RequestFor(3, hotVyjadreniId: 300L, datum: new DateTime(2026, 5, 1)),
            CancellationToken.None);

        result.PrimaryVazbaId.Should().BePositive();

        var activeBindings = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Stav == (byte)VazbaStav.Active).ToListAsync();

        activeBindings.Should().ContainSingle(b => b.Poradi == 3 && b.HotVyjadreniId == 300L);
        activeBindings.Should().NotContain(b => b.Poradi == 6);

        // Superseded stará K6
        var supersededK6 = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Poradi == 6 && x.Stav == (byte)VazbaStav.Superseded).ToListAsync();
        supersededK6.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateBindingAsync_WhenLaterStepAlreadyConsistent_NoCascade()
    {
        // K7=200@8/1 už respektuje chronologii. Target K3 = 5/1, K6 empty. Cascade nemá
        // co řešit pro K7 (> 5/1 OK), a K6 nemá kandidáta → buffer (null → žádný řádek).
        // Očekávání: K7 binding zůstává beze změny (žádný nový cascade update).
        await using var db = NewDb();
        await SeedBaseAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = ZaznamId,
            ExterniOdkazId = ExterniOdkazId,
            Poradi = 7,
            HotVyjadreniId = 200L,
            DatumVyjadreni = new DateTime(2026, 8, 1),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 1)
        });
        await db.SaveChangesAsync();

        var vq = MockAvailableBubbles((200L, new DateTime(2026, 8, 1)));
        var sut = BuildSut(db, vq.Object);

        var result = await sut.CreateBindingAsync(
            RequestFor(3, hotVyjadreniId: 300L, datum: new DateTime(2026, 5, 1)),
            CancellationToken.None);

        result.PrimaryVazbaId.Should().BePositive();

        // K7 binding musí zůstat aktivní a beze změny (stejný HotVyjadreniId, žádné superseded).
        var k7Rows = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Poradi == 7).ToListAsync();
        k7Rows.Should().ContainSingle();
        k7Rows[0].Stav.Should().Be((byte)VazbaStav.Active);
        k7Rows[0].HotVyjadreniId.Should().Be(200L);
    }

    [Fact]
    public async Task CreateBindingAsync_WhenTargetStepAlreadyHasBubble_SupersedesOldAndCreatesNew()
    {
        // K6 už má bublinu 100. User drag-nuje novou bublinu 500 na K6. Očekávání:
        // stará 100 → Superseded, nová 500 → Active (Manual).
        await using var db = NewDb();
        await SeedBaseAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = ZaznamId,
            ExterniOdkazId = ExterniOdkazId,
            Poradi = 6,
            HotVyjadreniId = 100L,
            DatumVyjadreni = new DateTime(2026, 3, 1),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 1)
        });
        await db.SaveChangesAsync();

        var vq = MockAvailableBubbles(
            (100L, new DateTime(2026, 3, 1)),
            (500L, new DateTime(2026, 5, 1)));
        var sut = BuildSut(db, vq.Object);

        var result = await sut.CreateBindingAsync(
            RequestFor(6, hotVyjadreniId: 500L, datum: new DateTime(2026, 5, 1)),
            CancellationToken.None);

        result.PrimaryVazbaId.Should().BePositive();

        var k6Rows = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Poradi == 6).ToListAsync();
        k6Rows.Should().HaveCount(2);
        k6Rows.Should().Contain(b => b.HotVyjadreniId == 100L && b.Stav == (byte)VazbaStav.Superseded);
        k6Rows.Should().Contain(b => b.HotVyjadreniId == 500L && b.Stav == (byte)VazbaStav.Active
                                  && b.Source == (byte)VazbaSource.Manual);
    }

    [Fact]
    public async Task CreateBindingAsync_SameBubbleAlreadyActiveOnTarget_IsIdempotent()
    {
        // Re-drop téže bubliny na tentýž krok nesmí vytvořit duplicitní Active řádek.
        await using var db = NewDb();
        await SeedBaseAsync(db);
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = ZaznamId,
            ExterniOdkazId = ExterniOdkazId,
            Poradi = 6,
            HotVyjadreniId = 500L,
            DatumVyjadreni = new DateTime(2026, 5, 1),
            Source = (byte)VazbaSource.Manual,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 1)
        });
        await db.SaveChangesAsync();

        var vq = MockAvailableBubbles((500L, new DateTime(2026, 5, 1)));
        var sut = BuildSut(db, vq.Object);

        await sut.CreateBindingAsync(
            RequestFor(6, hotVyjadreniId: 500L, datum: new DateTime(2026, 5, 1)),
            CancellationToken.None);

        var active = await db.VyjadreniVazby.AsNoTracking()
            .Where(x => x.Poradi == 6 && x.Stav == (byte)VazbaStav.Active).ToListAsync();
        active.Should().ContainSingle("re-drop téže bubliny nesmí duplicitně zaktivnit další řádek");
    }

    [Fact]
    public async Task CreateBindingAsync_UnknownKrok_ReturnsInvalidKrok()
    {
        await using var db = NewDb();
        await SeedBaseAsync(db);
        var vq = MockAvailableBubbles();
        var sut = BuildSut(db, vq.Object);

        var result = await sut.CreateBindingAsync(
            RequestFor(99, hotVyjadreniId: 100L, datum: new DateTime(2026, 3, 1)),
            CancellationToken.None);

        result.Outcome.Should().Be(BindingRebalanceOutcome.InvalidKrok);
        result.PrimaryVazbaId.Should().BeNull();

        var any = await db.VyjadreniVazby.AsNoTracking().AnyAsync();
        any.Should().BeFalse("neznámý krok nesmí vytvořit žádný řádek");
    }

    [Fact]
    public async Task CreateBindingAsync_MissingExterniOdkaz_ReturnsNotFound()
    {
        await using var db = NewDb();
        await SeedBaseAsync(db);
        var vq = MockAvailableBubbles();
        var sut = BuildSut(db, vq.Object);

        var req = new BindingRebalanceRequest(
            ZaznamId: ZaznamId,
            ExterniOdkazId: 99999, // neexistuje
            KrokPoradi: 6,
            HotVyjadreniId: 100L,
            DatumVyjadreni: new DateTime(2026, 3, 1),
            OsobaId: 1);

        var result = await sut.CreateBindingAsync(req, CancellationToken.None);
        result.Outcome.Should().Be(BindingRebalanceOutcome.ExterniOdkazNotFound);
        result.PrimaryVazbaId.Should().BeNull();
    }

    /// <summary>
    /// H-1: cascade updates nesmí mutovat bindingy jiného externího odkazu téhož záznamu.
    /// Záznam má 2 externí odkazy, oba mají K6 Active vazby. Drag na K3 externího odkazu A
    /// smí cascadnout jen v rámci A, B zůstává beze změny.
    /// </summary>
    [Fact]
    public async Task CreateBindingAsync_CascadesOnlyWithinTargetExterniOdkaz()
    {
        await using var db = NewDb();
        await SeedBaseAsync(db);
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = ExterniOdkazId + 1,
            ZaznamId = ZaznamId,
            Cislo = "654321"
        });
        // K6 binding na externím odkazu B — musí zůstat beze změny.
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            ZaznamId = ZaznamId,
            ExterniOdkazId = ExterniOdkazId + 1,
            Poradi = 6,
            HotVyjadreniId = 999L,
            DatumVyjadreni = new DateTime(2026, 3, 1),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 1)
        });
        await db.SaveChangesAsync();

        var vq = MockAvailableBubbles();
        var sut = BuildSut(db, vq.Object);

        await sut.CreateBindingAsync(
            RequestFor(3, hotVyjadreniId: 300L, datum: new DateTime(2026, 5, 1)),
            CancellationToken.None);

        var bRow = await db.VyjadreniVazby.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExterniOdkazId == ExterniOdkazId + 1);
        bRow.Should().NotBeNull();
        bRow!.Stav.Should().Be((byte)VazbaStav.Active);
        bRow.HotVyjadreniId.Should().Be(999L);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
