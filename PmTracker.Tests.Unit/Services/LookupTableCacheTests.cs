using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services;

namespace PmTracker.Tests.Unit.Services;

/// <summary>
/// Perf fix: scoped cache pro číselník tabulky musí držet první výsledek
/// po celou dobu requestu (zabránit 3-4× stejnému SELECT * FROM ciselnik_*
/// při renderingu detailu projektu).
/// </summary>
public sealed class LookupTableCacheTests
{
    [Fact]
    public async Task GetCategoriesAsync_CachesFirstResult_SecondCallReturnsOriginalEvenAfterDbMutation()
    {
        await using var db = CreateDb();
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 1,
            Kod = "U",
            Nazev = "Úkol"
        });
        await db.SaveChangesAsync();

        var cache = new LookupTableCache(db);

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
    public async Task NewCacheInstance_StartsFresh_DoesNotShareStateWithPreviousInstance()
    {
        await using var db = CreateDb();
        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 1,
            Kod = "U",
            Nazev = "Úkol"
        });
        await db.SaveChangesAsync();

        var cacheA = new LookupTableCache(db);
        var firstA = await cacheA.GetCategoriesAsync(CancellationToken.None);

        db.CiselnikKategoriiZaznamu.Add(new CiselnikKategoriiZaznamuEntity
        {
            Id = 2,
            Kod = "P",
            Nazev = "Projekt"
        });
        await db.SaveChangesAsync();

        // Druhá instance cache = jiný request = čerstvé čtení.
        var cacheB = new LookupTableCache(db);
        var firstB = await cacheB.GetCategoriesAsync(CancellationToken.None);

        firstA.Should().HaveCount(1);
        firstB.Should().HaveCount(2, "nová instance musí znovu načíst aktuální data");
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

        var cache = new LookupTableCache(db);
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
        var cache = new LookupTableCache(db);

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
