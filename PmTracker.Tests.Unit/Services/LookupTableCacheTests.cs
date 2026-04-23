using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services;

namespace PmTracker.Tests.Unit.Services;

/// <summary>
/// Perf fix: lookup cache je tenká scoped fasáda nad sdíleným singleton
/// <see cref="IMemoryCache"/> s TTL. Číselníky se načtou jednou a přežijí
/// přes request boundaries — horká cesta (Projekty/Detail + BuildZaznamEditAsync)
/// nevolá DB vůbec. Per-request memo navíc zajistí identity property
/// (<c>BeSameAs</c>) uvnitř jednoho requestu.
/// </summary>
public sealed class LookupTableCacheTests
{
    [Fact]
    public async Task GetCategoriesAsync_CachesFirstResult_SecondCallReturnsOriginalEvenAfterDbMutation()
    {
        var memory = new MemoryCache(new MemoryCacheOptions());
        await using var db = CreateDb();
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 1,
            Kod = "U",
            Nazev = "Úkol"
        });
        await db.SaveChangesAsync();

        var cache = new LookupTableCache(db, memory);

        // Prvotní načtení: cache si stáhne data.
        var first = await cache.GetCategoriesAsync(CancellationToken.None);
        first.Should().HaveCount(1);
        first[1].Nazev.Should().Be("Úkol");

        // Mezitím přímo v DB přidáme další řádek — pokud se cache znovu dotazuje,
        // uvidí 2 řádky. Pokud cachuje správně, uvidí pořád 1.
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 2,
            Kod = "P",
            Nazev = "Projekt"
        });
        await db.SaveChangesAsync();

        var second = await cache.GetCategoriesAsync(CancellationToken.None);
        second.Should().HaveCount(1, "cache musí vrátit původní snapshot, ne nová data");
        second.Should().BeSameAs(first, "druhé volání musí vrátit identickou dictionary instance");
    }

    [Fact]
    public async Task GetCategoriesAsync_PersistsAcrossCacheInstances_WhenSharedMemoryCache()
    {
        // NEW CONTRACT: cache žije v singleton IMemoryCache s TTL, nikoli per-request.
        // Druhá instance LookupTableCache (= další HTTP request) s tímtéž
        // sdíleným memcache musí dostat tentýž snapshot bez nového DB dotazu.
        var sharedMemory = new MemoryCache(new MemoryCacheOptions());
        await using var db = CreateDb();
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 1,
            Kod = "U",
            Nazev = "Úkol"
        });
        await db.SaveChangesAsync();

        var cache1 = new LookupTableCache(db, sharedMemory);
        var first = await cache1.GetCategoriesAsync(CancellationToken.None);

        // Simulujeme mezi-requestovou DB mutaci:
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 2,
            Kod = "P",
            Nazev = "Projekt"
        });
        await db.SaveChangesAsync();

        // Další request = nová instance cache ale stejný singleton memcache:
        var cache2 = new LookupTableCache(db, sharedMemory);
        var second = await cache2.GetCategoriesAsync(CancellationToken.None);

        second.Should().BeSameAs(first,
            "shared IMemoryCache musí vrátit tentýž snapshot — nové DB řádky se projeví až po TTL expiraci");
        second.Should().HaveCount(1);
    }

    [Fact]
    public async Task SeparateMemoryCaches_IsolateState_NewInstanceReadsFresh()
    {
        // Sanity: když dva registry mají *různý* memcache (nereálné v praxi,
        // ale důležité pro testovou izolaci), state se nesdílí.
        await using var db = CreateDb();
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity { Id = 1, Kod = "U", Nazev = "Úkol" });
        await db.SaveChangesAsync();

        var cacheA = new LookupTableCache(db, new MemoryCache(new MemoryCacheOptions()));
        var firstA = await cacheA.GetCategoriesAsync(CancellationToken.None);

        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity { Id = 2, Kod = "P", Nazev = "Projekt" });
        await db.SaveChangesAsync();

        var cacheB = new LookupTableCache(db, new MemoryCache(new MemoryCacheOptions()));
        var firstB = await cacheB.GetCategoriesAsync(CancellationToken.None);

        firstA.Should().HaveCount(1);
        firstB.Should().HaveCount(2, "jiný memcache = čerstvé čtení z DB");
        firstA.Should().NotBeSameAs(firstB);
    }

    [Fact]
    public async Task GetMultipleLookupTables_EachCachedIndependently()
    {
        await using var db = CreateDb();
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity { Id = 1, Kod = "U", Nazev = "Úkol" });
        db.CiselnikStavuUkolu.Add(new CiselnikStavuUkoluEntity { Id = 1, Kod = "OPEN", Nazev = "Otevřeno" });
        db.CiselnikTypuUkolu.Add(new CiselnikTypuUkoluEntity { Id = 1, Kod = "RU", Nazev = "Rozvoj" });
        await db.SaveChangesAsync();

        var cache = new LookupTableCache(db, new MemoryCache(new MemoryCacheOptions()));
        var categories = await cache.GetCategoriesAsync(CancellationToken.None);
        var states = await cache.GetTaskStatesAsync(CancellationToken.None);
        var types = await cache.GetTaskTypesAsync(CancellationToken.None);

        categories.Should().HaveCount(1);
        states.Should().HaveCount(1);
        types.Should().HaveCount(1);
        categories[1].Nazev.Should().Be("Úkol");
        states[1].Nazev.Should().Be("Otevřeno");
        types[1].Nazev.Should().Be("Rozvoj");
    }

    [Fact]
    public async Task EmptyTable_ReturnsEmptyDictionary_AndIsStillCached()
    {
        await using var db = CreateDb();
        var cache = new LookupTableCache(db, new MemoryCache(new MemoryCacheOptions()));

        var first = await cache.GetCategoriesAsync(CancellationToken.None);
        first.Should().BeEmpty();

        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity { Id = 1, Kod = "U", Nazev = "Úkol" });
        await db.SaveChangesAsync();

        var second = await cache.GetCategoriesAsync(CancellationToken.None);
        second.Should().BeEmpty("empty dictionary musí zůstat v cache — není to sentinel 'not yet loaded'");
    }

    private static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }
}
