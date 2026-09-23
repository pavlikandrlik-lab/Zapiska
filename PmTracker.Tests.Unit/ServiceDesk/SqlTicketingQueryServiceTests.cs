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
    public async Task GetAkceptovanouKalkulaci_FiltrujeAkceptovano_VraciNejvyssiId()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "336865", Akceptace = "Návrh", Cena = 100 },
            new HotKalkulaceEntity { Id = 2, Pid = "336865", Akceptace = "Akceptováno", Cena = 200 },
            new HotKalkulaceEntity { Id = 3, Pid = "336865", Akceptace = "Akceptováno", Cena = 300 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(3);
        result.CenaCelkem.Should().Be(300);
    }

    /// <summary>
    /// Když má PNF víc akceptovaných kalkulací, vyhrává ta s vyšším id (rozhodnutí
    /// uživatele 2026-09-08). Dřív se řadilo podle sloupce `verze`, jenže ten nese
    /// označení technického zadání ("TZFIS2026") a bývá u všech kalkulací PNF stejný —
    /// „nejnovější" tak vycházela náhodně a do výzvy mohly jít ceny ze špatné kalkulace.
    ///
    /// Data zrcadlí skutečný vzorek z produkce: nejvyšší id je NEakceptované, takže
    /// se musí vybrat nižší, ale akceptované.
    /// </summary>
    [Fact]
    public async Task GetAkceptovanouKalkulaci_VybereNejvyssiIdZAkceptovanych()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "336865", Akceptace = "Akceptováno", Cena = 100 },
            new HotKalkulaceEntity { Id = 2, Pid = "336865", Akceptace = "Akceptováno", Cena = 200 },
            new HotKalkulaceEntity { Id = 3, Pid = "336865", Akceptace = "Neakceptováno", Cena = 300 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(2, "z akceptovaných vyhrává vyšší id, ne vyšší verze");
        result.CenaCelkem.Should().Be(200);
    }

    /// <summary>Totéž pro dávkovou variantu, kterou používá tisk výzvy.</summary>
    [Fact]
    public async Task GetAkceptovaneKalkulace_PriViceAkceptovanych_VybereVyssiId()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "A490P00ECYL1", Akceptace = "Akceptováno", Cena = 100 },
            new HotKalkulaceEntity { Id = 2, Pid = "A490P00ECYL1", Akceptace = "Akceptováno", Cena = 200 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovaneKalkulaceAsync(
            new[] { "A490P00ECYL1" }, CancellationToken.None);

        result["A490P00ECYL1"].CenaCelkem.Should().Be(200, "vyšší id vyhrává i v dávce");
    }

    [Fact]
    public async Task GetAkceptovanouKalkulaci_ZadnaAkceptovana_VraciNull()
    {
        using var db = CreateDb();
        db.HotKalkulace.Add(new HotKalkulaceEntity { Id = 10, Pid = "111111", Akceptace = "Návrh", Cena = 100 });
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
            new HotKalkulaceEntity { Id = 1, Pid = "A", Akceptace = "Akceptováno", Cena = 10 },
            new HotKalkulaceEntity { Id = 2, Pid = "B", Akceptace = "Akceptováno", Cena = 20 },
            new HotKalkulaceEntity { Id = 3, Pid = "C", Akceptace = "Návrh", Cena = 30 });
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
        // Reálná intranetNEW.dbo.HOT_ZAZNAMY.id je INT — testovací Id musí být číselný
        // string (parsovatelný int.Parse), jinak ValueConverter v TicketingReadOnlyDbContext
        // hodí FormatException. Memory: SD ticket bez id = mimo scope.
        using var db = CreateDb();
        db.HotZaznamy.AddRange(
            new HotZaznamEntity { Radek = 1, Id = "111111", Strucne = "foo" },
            new HotZaznamEntity { Radek = 2, Id = "222222", Strucne = "bar" });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetZaznamyAsync(new[] { "111111", "222222", "999999" }, CancellationToken.None);

        result.Should().HaveCount(2);
        result["111111"].Strucne.Should().Be("foo");
        result.ContainsKey("999999").Should().BeFalse();
    }

    [Fact]
    public void SaveChanges_Throws_ProtiZapisu()
    {
        using var db = CreateDb();
        var act = () => db.SaveChanges();
        act.Should().Throw<InvalidOperationException>().WithMessage("*read-only*");
    }

    /// <summary>
    /// Akceptace není jedna hodnota, ale zákazový seznam (zadání uživatele 2026-09-09):
    /// akceptovaná je každá hodnota kromě „Návrh", „Neakceptováno" a „Akceptovat ?".
    ///
    /// Vzorek z produkce, který vadu odhalil: kalkulace se stavem „Fakturovat", u níž
    /// vyjadreni_kalk výslovně uvádí akceptaci projektovým manažerem. Dřívější filtr
    /// akceptace == „Akceptováno" ji zahodil, takže by se u PNF vytiskla nulová cena.
    /// </summary>
    [Theory]
    [InlineData("Fakturovat")]
    [InlineData("Akceptováno")]
    [InlineData("Nabídka")]
    [InlineData("Vyfakturováno")]
    public async Task GetAkceptovanouKalkulaci_BereIJineStavyNezAkceptovano(string stav)
    {
        using var db = CreateDb();
        db.HotKalkulace.Add(new HotKalkulaceEntity
        {
            Id = 5414, Pid = "A490P00E21MX", Akceptace = stav, Cena = 10340m, CenaL = 10340m,
        });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("A490P00E21MX", CancellationToken.None);

        result.Should().NotBeNull($"stav „{stav}“ není na zákazovém seznamu");
        result!.CenaCelkem.Should().Be(10340m);
    }

    /// <summary>
    /// Vyloučené stavy nesmí kalkulaci vybrat ani tehdy, když mají nejvyšší id —
    /// jinak by rozpracovaný návrh přebil skutečně akceptovanou kalkulaci.
    /// </summary>
    [Theory]
    [InlineData("Návrh")]
    [InlineData("Neakceptováno")]
    [InlineData("Akceptovat ?")]
    public async Task GetAkceptovanouKalkulaci_VynechavaRozpracovaneAOdmitnute(string stav)
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "336865", Akceptace = "Fakturovat", Cena = 100 },
            new HotKalkulaceEntity { Id = 2, Pid = "336865", Akceptace = stav, Cena = 999 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().NotBeNull();
        result!.CenaCelkem.Should().Be(100, $"„{stav}“ se nesmí vybrat ani s vyšším id");
    }

    /// <summary>
    /// ServiceDesk píše stav textově a postupně, takže se v datech objevují drobné
    /// odchylky. Porovnání proto ignoruje bílé znaky okolo i velikost písmen —
    /// jinak by „akceptovat ?" s mezerou navíc prošlo jako akceptované.
    /// </summary>
    [Theory]
    [InlineData("  Akceptovat ?  ")]
    [InlineData("NEAKCEPTOVÁNO")]
    [InlineData("návrh")]
    public async Task GetAkceptovanouKalkulaci_VyloucenyStav_PoznaIVJinemZapisu(string stav)
    {
        using var db = CreateDb();
        db.HotKalkulace.Add(new HotKalkulaceEntity { Id = 1, Pid = "336865", Akceptace = stav, Cena = 100 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().BeNull($"„{stav}“ je zápisová varianta vyloučeného stavu");
    }

    /// <summary>
    /// Nevyplněná akceptace není akceptace. ServiceDesk stav dopisuje průběžně, takže
    /// prázdná hodnota znamená „zatím se nic nestalo" — a cenu z takové kalkulace
    /// nemá výzva co tisknout.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAkceptovanouKalkulaci_PrazdnaAkceptace_SeNepocita(string? stav)
    {
        using var db = CreateDb();
        db.HotKalkulace.Add(new HotKalkulaceEntity { Id = 1, Pid = "336865", Akceptace = stav, Cena = 100 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovanouKalkulaciAsync("336865", CancellationToken.None);

        result.Should().BeNull("nevyplněná akceptace není akceptace");
    }

    /// <summary>Zákazový seznam platí i pro dávkovou variantu, kterou používá tisk výzvy.</summary>
    [Fact]
    public async Task GetAkceptovaneKalkulace_Batch_RespektujeZakazovySeznam()
    {
        using var db = CreateDb();
        db.HotKalkulace.AddRange(
            new HotKalkulaceEntity { Id = 1, Pid = "A", Akceptace = "Fakturovat", Cena = 10 },
            new HotKalkulaceEntity { Id = 2, Pid = "A", Akceptace = "Návrh", Cena = 999 },
            new HotKalkulaceEntity { Id = 3, Pid = "B", Akceptace = "Akceptovat ?", Cena = 20 });
        db.SaveChangesForTests();

        var svc = new SqlTicketingQueryService(db);
        var result = await svc.GetAkceptovaneKalkulaceAsync(new[] { "A", "B" }, CancellationToken.None);

        result["A"].CenaCelkem.Should().Be(10, "návrh s vyšším id se nesmí vybrat");
        result.ContainsKey("B").Should().BeFalse("PNF bez akceptované kalkulace v seznamu chybí");
    }
}
