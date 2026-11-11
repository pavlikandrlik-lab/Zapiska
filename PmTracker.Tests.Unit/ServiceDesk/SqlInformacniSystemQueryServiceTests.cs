using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SqlInformacniSystemQueryServiceTests
{
    private static TicketingReadOnlyDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<TicketingReadOnlyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TicketingReadOnlyDbContext(opts);
    }

    [Fact]
    public async Task GetAktivniIsAsync_VraciJenAktivni_IgnorujeTrailingSpaces()
    {
        using var db = CreateDb();
        db.HotIs.AddRange(
            new HotIsEntity { Id = 1, Nazev = "Finanční IS",    Zkratka = "FIS",   Aktivita = "Aktivní   ", Limit = 100, Cerpani = 50 },
            new HotIsEntity { Id = 2, Nazev = "Personální IS",  Zkratka = "ISSP",  Aktivita = "Aktivní   ", Limit = 200, Cerpani = 20 },
            new HotIsEntity { Id = 3, Nazev = "Vyřazený",        Zkratka = "OLD",   Aktivita = "Neaktivní", Limit = null, Cerpani = null });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetAktivniIsAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Zkratka).Should().BeEquivalentTo(new[] { "FIS", "ISSP" });
        result.All(x => x.JeAktivni).Should().BeTrue();
    }

    [Fact]
    public async Task GetAktivniIsAsync_PrazdnaDb_VraciPrazdnyList()
    {
        using var db = CreateDb();
        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetAktivniIsAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRozpocetAsync_VraciLimitACerpani()
    {
        using var db = CreateDb();
        db.HotIs.Add(new HotIsEntity { Id = 1, Zkratka = "FIS", Aktivita = "Aktivní",
                                        Limit = 1000m, Cerpani = 750m });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetRozpocetAsync(1, CancellationToken.None);

        result.Should().NotBeNull();
        result!.IsId.Should().Be(1);
        result.IsZkratka.Should().Be("FIS");
        result.Limit.Should().Be(1000m);
        result.Cerpani.Should().Be(750m);
        result.ProcentoCerpani.Should().Be(75.0m);
    }

    [Fact]
    public async Task GetRozpocetAsync_BezLimitu_ProcentoNull()
    {
        using var db = CreateDb();
        db.HotIs.Add(new HotIsEntity { Id = 1, Zkratka = "FIS", Aktivita = "Aktivní",
                                        Limit = null, Cerpani = 500m });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetRozpocetAsync(1, CancellationToken.None);

        result!.ProcentoCerpani.Should().BeNull();
    }

    [Fact]
    public async Task GetRozpocetAsync_NeznamyIs_VraciNull()
    {
        using var db = CreateDb();
        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetRozpocetAsync(999, CancellationToken.None);
        result.Should().BeNull();
    }
}
