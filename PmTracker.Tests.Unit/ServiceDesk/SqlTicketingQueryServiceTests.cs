using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SqlTicketingQueryServiceTests
{
    private static TicketingReadOnlyDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TicketingReadOnlyDbContext(opts);
    }

    [Fact]
    public async Task GetAkceptovanouKalkulaci_FiltrujeAkceptovano_VraciNejvyssiVerzi()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "336865", Verze = 1, Akceptace = "Nabídka", Cena = 100 },
            new HotKalkulaceEntity { Id = 2, Pid = "336865", Verze = 2, Akceptace = "Akceptováno", Cena = 200 },
            new HotKalkulaceEntity { Id = 3, Pid = "336865", Verze = 3, Akceptace = "Akceptováno", Cena = 300 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(3);
        result.CenaCelkem.Should().Be(300);
    }

    [Fact]
    public async Task GetAkceptovanouKalkulaci_ZadnaAkceptovana_VraciNull()
    {
        using var db = CreateDb();
        db.HotKalkulace.Add(new HotKalkulaceEntity { Id = 10, Pid = "111111", Verze = 1, Akceptace = "Nabídka", Cena = 100 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("111111", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAkceptovaneKalkulace_Batch_VraciPerPid()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "A", Verze = 1, Akceptace = "Akceptováno", Cena = 10 },
            new HotKalkulaceEntity { Id = 2, Pid = "B", Verze = 1, Akceptace = "Akceptováno", Cena = 20 },
            new HotKalkulaceEntity { Id = 3, Pid = "C", Verze = 1, Akceptace = "Nabídka", Cena = 30 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovaneKalkulaceAsync(new[] { "A", "B", "C" }, CancellationToken.None);

        result.Should().HaveCount(2);
        result["A"].CenaCelkem.Should().Be(10);
        result["B"].CenaCelkem.Should().Be(20);
        result.ContainsKey("C").Should().BeFalse();
    }

    [Fact]
    public async Task GetZaznamy_Batch_VraciExistujiciIgnorujeNeznama()
    {
        using var db = CreateDb();
        db.HotZaznamy.AddRange(
            new HotZaznamEntity { Radek = 1, Id = "A", Strucne = "foo" },
            new HotZaznamEntity { Radek = 2, Id = "B", Strucne = "bar" });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetZaznamyAsync(new[] { "A", "B", "C" }, CancellationToken.None);

        result.Should().HaveCount(2);
        result["A"].Strucne.Should().Be("foo");
        result.ContainsKey("C").Should().BeFalse();
    }

    [Fact]
    public void SaveChanges_Throws_ProtiZapisu()
    {
        using var db = CreateDb();
        var act = () => db.SaveChanges();
        act.Should().Throw<InvalidOperationException>().WithMessage("*read-only*");
    }
}
