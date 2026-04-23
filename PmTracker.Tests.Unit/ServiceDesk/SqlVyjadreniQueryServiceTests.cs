using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SqlVyjadreniQueryServiceTests
{
    private static TicketingReadOnlyDbContext NewInMemory()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase("tiketing-" + Guid.NewGuid())
            .Options;
        return new TicketingReadOnlyDbContext(opts);
    }

    // Helpers — entities jsou internal; přistupujeme přes reflection, protože Tests.Unit
    // má InternalsVisibleTo, ale typ `HotVyjadreniEntity` je ve ServiceDesk.Sql assembly.
    private static object NewZaznam(string id, string? pid)
    {
        var asm = typeof(TicketingReadOnlyDbContext).Assembly;
        var type = asm.GetType("PmTracker.ServiceDesk.Sql.Entities.HotZaznamEntity", throwOnError: true)!;
        var obj = Activator.CreateInstance(type, nonPublic: true)!;
        SetProp(obj, "Id", id);
        SetProp(obj, "Pid", pid);
        return obj;
    }

    private static object NewVyjadreni(long id, string pid, DateTime datum, string? popis = null, string? zpracoval = null, string? typ = null, string? tym = null)
    {
        var asm = typeof(TicketingReadOnlyDbContext).Assembly;
        var type = asm.GetType("PmTracker.ServiceDesk.Sql.Entities.HotVyjadreniEntity", throwOnError: true)!;
        var obj = Activator.CreateInstance(type, nonPublic: true)!;
        SetProp(obj, "Id", id);
        SetProp(obj, "Pid", pid);
        SetProp(obj, "Datum", (DateTime?)datum);
        SetProp(obj, "Popis", popis);
        SetProp(obj, "Zpracoval", zpracoval);
        SetProp(obj, "Typ", typ);
        SetProp(obj, "Tym", tym);
        return obj;
    }

    private static void SetProp(object obj, string name, object? value)
    {
        var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        p!.SetValue(obj, value);
    }

    private static async Task SeedAsync(TicketingReadOnlyDbContext db, IEnumerable<object> entities)
    {
        foreach (var e in entities)
        {
            db.Add(e);
        }
        // SaveChangesForTests bypasses the read-only guard
        var method = typeof(TicketingReadOnlyDbContext).GetMethod("SaveChangesForTests", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(db, null);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_ReturnsOnlyMatchingTicketAndSortsAsc()
    {
        await using var db = NewInMemory();
        await SeedAsync(db, new[]
        {
            NewZaznam("336865", "A400P023RVVP"),
            NewZaznam("999999", "ZZZZZZ"),
            NewVyjadreni(1, "A400P023RVVP", new DateTime(2026, 1, 5), "prvni"),
            NewVyjadreni(2, "A400P023RVVP", new DateTime(2026, 1, 10), "druhe"),
            NewVyjadreni(3, "ZZZZZZ", new DateTime(2026, 1, 7), "jiny tiket"),
        });

        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("336865", sinceUtc: null, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[1].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_WithSinceUtc_FiltersIncrementalOnly()
    {
        await using var db = NewInMemory();
        await SeedAsync(db, new[]
        {
            NewZaznam("336865", "A400P023RVVP"),
            NewVyjadreni(1, "A400P023RVVP", new DateTime(2026, 1, 5)),
            NewVyjadreni(2, "A400P023RVVP", new DateTime(2026, 1, 10)),
        });

        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("336865", new DateTime(2026, 1, 6), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_EmptyCislo_ReturnsEmpty()
    {
        await using var db = NewInMemory();
        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("", null, CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_DeterministickeRazeni_StejneDatumRadiPodleId()
    {
        // Při bulk inserts na SD straně často vzniká více vyjádření se stejným datum;
        // SQL Server default ordering je undefined → v chat modalu občas přehozené bubliny.
        // Sekundární ThenBy(v.Id) musí garantovat stabilní výstup.
        await using var db = NewInMemory();
        var sameDatum = new DateTime(2026, 4, 23, 14, 29, 0);
        await SeedAsync(db, new[]
        {
            NewZaznam("111111", "P1"),
            NewVyjadreni(300L, "P1", sameDatum, "third"),
            NewVyjadreni(100L, "P1", sameDatum, "first"),
            NewVyjadreni(200L, "P1", sameDatum, "second"),
        });

        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("111111", sinceUtc: null, CancellationToken.None);

        result.Select(v => v.Id).Should().Equal(100L, 200L, 300L);
    }

    [Fact]
    public async Task GetVyjadreniForTicketAsync_SkipsRowsWithNullDatum()
    {
        await using var db = NewInMemory();
        var v = NewVyjadreni(99, "A400P023RVVP", new DateTime(2026, 1, 5));
        // Null out datum to simulate corrupt row
        var p = v.GetType().GetProperty("Datum", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)!;
        p.SetValue(v, null);
        await SeedAsync(db, new[]
        {
            NewZaznam("336865", "A400P023RVVP"),
            v,
        });

        var sut = new SqlVyjadreniQueryService(db);
        var result = await sut.GetVyjadreniForTicketAsync("336865", null, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
