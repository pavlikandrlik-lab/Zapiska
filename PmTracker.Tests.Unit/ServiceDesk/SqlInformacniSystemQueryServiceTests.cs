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

    private static void SeedIsHierarchy(TicketingReadOnlyDbContext db)
    {
        db.HotIs.Add(new HotIsEntity { Id = 1, Zkratka = "FIS", Aktivita = "Aktivní   " });
        db.HotModuly.AddRange(
            new HotModulyEntity { Id = 100, Zkratka = "R_EIS", Subsystem = "DVEIS", IdIS = 1, Aktivita = "Aktivní" },
            new HotModulyEntity { Id = 101, Zkratka = "TP_RZA", Subsystem = "TPFIS", IdIS = 1, Aktivita = "Aktivní" },
            new HotModulyEntity { Id = 200, Zkratka = "JINY_IS", Subsystem = "XXX", IdIS = 99, Aktivita = "Aktivní" }); // mimo náš IS
    }

    [Fact]
    public async Task GetProdleneAsync_NES_UzivaSlaDeadline_FiltrujeStav()
    {
        var reference = new DateTime(2026, 4, 23, 12, 0, 0);

        using var db = CreateDb();
        SeedIsHierarchy(db);
        db.HotZaznamy.AddRange(
            // v prodlení u dodavatele — MUSÍ SE VRÁTIT
            new HotZaznamEntity { Radek = 1, Id = "111111", Pid = "P1", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "dodavatel", SlaDeadline = reference.AddDays(-5) },
            // v prodlení ale archiv — IGNOROVAT
            new HotZaznamEntity { Radek = 2, Id = "222222", Pid = "P2", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "archiv", SlaDeadline = reference.AddDays(-10) },
            // NES bez SLA — IGNOROVAT (NES musí mít sla_deadline)
            new HotZaznamEntity { Radek = 3, Id = "333333", Pid = "P3", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "otevřeno", SlaDeadline = null },
            // SLA v budoucnu — IGNOROVAT
            new HotZaznamEntity { Radek = 4, Id = "444444", Pid = "P4", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "otevřeno", SlaDeadline = reference.AddDays(5) },
            // modul mimo náš IS — IGNOROVAT
            new HotZaznamEntity { Radek = 5, Id = "555555", Pid = "P5", TypZaznamu = "NES", Modul = "JINY_IS",
                                  Stav = "dodavatel", SlaDeadline = reference.AddDays(-1) });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(111111);
        result[0].TypZaznamu.Should().Be("NES");
        result[0].DniProdleni.Should().Be(5);
        result[0].Termin.Should().Be(reference.AddDays(-5));
    }

    [Fact]
    public async Task GetProdleneAsync_PMP_UzivaDatResT_IgnorujeSlaDeadline()
    {
        var reference = new DateTime(2026, 4, 23, 12, 0, 0);

        using var db = CreateDb();
        SeedIsHierarchy(db);
        db.HotZaznamy.AddRange(
            new HotZaznamEntity { Radek = 1, Id = "100001", Pid = "P1", TypZaznamu = "PMP", Modul = "TP_RZA",
                                  Stav = "dodavatel", DatResT = reference.AddDays(-3), SlaDeadline = null },
            new HotZaznamEntity { Radek = 2, Id = "100002", Pid = "P2", TypZaznamu = "PMP", Modul = "TP_RZA",
                                  Stav = "otevřeno", DatResT = reference.AddDays(5), SlaDeadline = null },
            new HotZaznamEntity { Radek = 3, Id = "100003", Pid = "P3", TypZaznamu = "PMP", Modul = "TP_RZA",
                                  Stav = "otevřeno", DatResT = null });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(100001);
        result[0].TypZaznamu.Should().Be("PMP");
        result[0].DniProdleni.Should().Be(3);
    }

    [Fact]
    public async Task GetProdleneAsync_PNF_UzivaDatResT_StejneJakoPmp()
    {
        var reference = new DateTime(2026, 4, 23, 12, 0, 0);

        using var db = CreateDb();
        SeedIsHierarchy(db);
        db.HotZaznamy.Add(
            new HotZaznamEntity { Radek = 1, Id = "200001", Pid = "P1", TypZaznamu = "PNF", Modul = "TP_RZA",
                                  Stav = "dodavatel", DatResT = reference.AddDays(-7), SlaDeadline = null });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].TypZaznamu.Should().Be("PNF");
        result[0].DniProdleni.Should().Be(7);
    }

    [Fact]
    public async Task GetProdleneAsync_RadiOdNejstarsichVProdleni()
    {
        var reference = new DateTime(2026, 4, 23, 12, 0, 0);

        using var db = CreateDb();
        SeedIsHierarchy(db);
        db.HotZaznamy.AddRange(
            new HotZaznamEntity { Radek = 1, Id = "111111", Pid = "P1", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "dodavatel", SlaDeadline = reference.AddDays(-2) },
            new HotZaznamEntity { Radek = 2, Id = "222222", Pid = "P2", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "dodavatel", SlaDeadline = reference.AddDays(-10) },
            new HotZaznamEntity { Radek = 3, Id = "333333", Pid = "P3", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "dodavatel", SlaDeadline = reference.AddDays(-5) });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

        // nejvíce v prodlení první
        result.Select(r => r.Id).Should().Equal(222222, 333333, 111111);
        result.Select(r => r.DniProdleni).Should().Equal(10, 5, 2);
    }

    [Fact]
    public async Task GetProdleneAsync_TicketBezId_IgnorovatNezobrazitJakoNula()
    {
        // Memory feedback: ticket bez HOT_ZAZNAMY.id je mimo scope PM Trackeru
        var reference = new DateTime(2026, 4, 23, 12, 0, 0);

        using var db = CreateDb();
        SeedIsHierarchy(db);
        db.HotZaznamy.AddRange(
            // chybí id — IGNOROVAT
            new HotZaznamEntity { Radek = 1, Id = "", Pid = "P1", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "dodavatel", SlaDeadline = reference.AddDays(-5) },
            // validní ticket
            new HotZaznamEntity { Radek = 2, Id = "777777", Pid = "P2", TypZaznamu = "NES", Modul = "R_EIS",
                                  Stav = "dodavatel", SlaDeadline = reference.AddDays(-3) });
        db.SaveChangesForTests();

        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetProdleneAsync(isId: 1, reference, CancellationToken.None);

        result.Should().HaveCount(1, "ticket bez id je mimo scope PM Trackeru");
        result[0].Id.Should().Be(777777);
        result.Should().NotContain(x => x.Id == 0, "NIKDY fallback na 0");
    }

    [Fact]
    public async Task GetProdleneAsync_NeznamyIs_VraciPrazdny()
    {
        using var db = CreateDb();
        SeedIsHierarchy(db);
        var svc = new SqlInformacniSystemQueryService(db);
        var result = await svc.GetProdleneAsync(isId: 999, DateTime.UtcNow, CancellationToken.None);
        result.Should().BeEmpty();
    }
}
