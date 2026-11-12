using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.ServiceDesk.Sql;
using PmTracker.ServiceDesk.Sql.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>Dotazy pro výpočet prodlení přes dvě databáze (spec 2026-10-15 §6.3, §7).</summary>
public sealed class SqlProdleniQueryServiceTests
{
    private const int IsId = 1;

    private static TicketingReadOnlyDbContext Intranet()
        => new(new DbContextOptionsBuilder<TicketingReadOnlyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ServiceDeskExternalReadOnlyDbContext Sd()
        => new(new DbContextOptionsBuilder<ServiceDeskExternalReadOnlyDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void SeedModuly(TicketingReadOnlyDbContext db)
    {
        db.HotModuly.AddRange(
            new HotModulyEntity { Id = 100, Zkratka = "R_EIS", IdIS = IsId },
            new HotModulyEntity { Id = 101, Zkratka = "TP_RZA", IdIS = IsId },
            new HotModulyEntity { Id = 200, Zkratka = "JINY", IdIS = 99 });
    }

    private static int _radek;
    private static HotZaznamEntity Zaznam(string id, string modul = "R_EIS", string typ = "NES  ", string stav = "dodavatel")
        => new() { Radek = Interlocked.Increment(ref _radek), Id = id, Pid = "P" + id, TypZaznamu = typ, Modul = modul,
                   Stav = stav, Dulezitost = "NES II", Dodavatel = "Dodavatel A", Strucne = "Popis " + id };

    private static TicketExternalEntity Externi(int id, string externalId, string? supplierTag = "DOD-A")
        => new() { Id = id, ExternalId = externalId, SupplierTag = supplierTag };

    private static TicketExternalWorkflowEntity Predavka(int id, int ticketExternalId, DateTime datum, int typ = 1)
        => new() { Id = id, TicketExternalId = ticketExternalId, Date = datum, TransferType = typ, Supplier = "Dodavatel A" };

    [Fact]
    public async Task Jen_tickety_is_se_zaznamem_v_ticket_external()
    {
        using var intranet = Intranet();
        using var sd = Sd();
        SeedModuly(intranet);
        intranet.HotZaznamy.AddRange(
            Zaznam("111111"),                // IS + ticket_external → ano
            Zaznam("222222"),                // IS bez ticket_external → ne
            Zaznam("333333", modul: "JINY")); // jiný IS → ne
        intranet.SaveChangesForTests();
        sd.TicketExternal.AddRange(Externi(1, "111111"), Externi(3, "333333"));
        sd.SaveChangesForTests();

        var vysledek = await new SqlProdleniQueryService(intranet, sd).GetSynchronizovaneTicketyAsync(IsId, null, default);

        vysledek.Select(t => t.Id).Should().Equal(111111);
        var t = vysledek[0];
        t.TicketExternalId.Should().Be(1);
        t.TypZaznamu.Should().Be("NES", "typ_zaznamu se vždy normalizuje Trim + ToUpperInvariant");
        t.SupplierTag.Should().Be("DOD-A");
        t.Dulezitost.Should().Be("NES II");
    }

    [Fact]
    public async Task External_id_s_mezerami_a_nenumericke()
    {
        using var intranet = Intranet();
        using var sd = Sd();
        SeedModuly(intranet);
        intranet.HotZaznamy.AddRange(Zaznam("123456"), Zaznam("654321"));
        intranet.SaveChangesForTests();
        sd.TicketExternal.AddRange(Externi(1, " 123456 "), Externi(2, "ABC"), Externi(3, "654321x"));
        sd.SaveChangesForTests();

        var vysledek = await new SqlProdleniQueryService(intranet, sd).GetSynchronizovaneTicketyAsync(IsId, null, default);

        vysledek.Select(t => (t.Id, t.TicketExternalId)).Should().Equal((123456, 1));
    }

    [Fact]
    public async Task Duplicitni_external_id_bere_nizsi_id()
    {
        using var intranet = Intranet();
        using var sd = Sd();
        SeedModuly(intranet);
        intranet.HotZaznamy.Add(Zaznam("123456"));
        intranet.SaveChangesForTests();
        sd.TicketExternal.AddRange(Externi(9, "123456"), Externi(4, "123456 "));
        sd.SaveChangesForTests();

        var vysledek = await new SqlProdleniQueryService(intranet, sd).GetSynchronizovaneTicketyAsync(IsId, null, default);

        vysledek.Should().ContainSingle().Which.TicketExternalId.Should().Be(4);
    }

    [Fact]
    public async Task Archivovane_jen_s_predavkou_od()
    {
        var od = new DateTime(2026, 1, 1);
        using var intranet = Intranet();
        using var sd = Sd();
        SeedModuly(intranet);
        intranet.HotZaznamy.AddRange(
            Zaznam("100001", stav: "archiv"),   // předávka v období → ano (jen s parametrem)
            Zaznam("100002", stav: "archiv"),   // předávky jen před obdobím → ne
            Zaznam("100003"));                  // nearchivovaný → vždy
        intranet.SaveChangesForTests();
        sd.TicketExternal.AddRange(Externi(1, "100001"), Externi(2, "100002"), Externi(3, "100003"));
        sd.TicketExternalWorkflow.AddRange(
            Predavka(10, 1, new DateTime(2025, 12, 20)), Predavka(11, 1, new DateTime(2026, 1, 5), typ: 3),
            Predavka(20, 2, new DateTime(2025, 6, 1)), Predavka(21, 2, new DateTime(2025, 7, 1), typ: 3));
        sd.SaveChangesForTests();
        var svc = new SqlProdleniQueryService(intranet, sd);

        (await svc.GetSynchronizovaneTicketyAsync(IsId, od, default)).Select(t => t.Id).Should().BeEquivalentTo([100001, 100003]);
        (await svc.GetSynchronizovaneTicketyAsync(IsId, null, default)).Select(t => t.Id).Should().Equal(100003);
    }

    [Fact]
    public async Task Ticket_bez_id_vyrazen()
    {
        using var intranet = Intranet();
        using var sd = Sd();
        SeedModuly(intranet);
        intranet.HotZaznamy.AddRange(Zaznam(""), Zaznam("111111"));
        intranet.SaveChangesForTests();
        sd.TicketExternal.AddRange(Externi(1, "111111"), Externi(2, "0"));
        sd.SaveChangesForTests();

        var vysledek = await new SqlProdleniQueryService(intranet, sd).GetSynchronizovaneTicketyAsync(IsId, null, default);

        vysledek.Select(t => t.Id).Should().Equal(111111);
    }

    [Fact]
    public async Task Workflow_chunkuje_nad_1000()
    {
        using var intranet = Intranet();
        using var sd = Sd();
        var ids = Enumerable.Range(1, 2500).ToList();
        sd.TicketExternal.AddRange(ids.Select(i => Externi(i, (100000 + i).ToString())));
        sd.TicketExternalWorkflow.AddRange(ids.Select(i => Predavka(i, i, new DateTime(2026, 3, 1).AddMinutes(i))));
        sd.SaveChangesForTests();

        var wf = await new SqlProdleniQueryService(intranet, sd).GetWorkflowAsync(ids, default);

        wf.Should().HaveCount(2500);
        wf[2500].Should().ContainSingle().Which.Should().Be(
            new WorkflowUdalostDto(2500, new DateTime(2026, 3, 1).AddMinutes(2500), 1, "Dodavatel A", null));
    }

    [Fact]
    public async Task Sla_katalog_cely()
    {
        using var intranet = Intranet();
        using var sd = Sd();
        sd.TicketExternalSla.AddRange(
            new TicketExternalSlaEntity { Id = 1, SlaName = "NES I", SlaHours = 8 },
            new TicketExternalSlaEntity { Id = 2, SlaName = "NES II ", SlaHours = 16.5 });
        sd.SaveChangesForTests();

        var katalog = await new SqlProdleniQueryService(intranet, sd).GetSlaKatalogAsync(default);

        // Ořez a párování dělá ProdleniService (spec R8); dotaz vrací hodnoty beze změny.
        katalog.Should().Equal(new SlaKatalogDto(1, "NES I", 8), new SlaKatalogDto(2, "NES II ", 16.5));
    }

    [Fact]
    public async Task Kalkulace_zakazovy_seznam_a_nejvyssi_verze()
    {
        var od = new DateTime(2026, 1, 1);
        var @do = new DateTime(2026, 12, 31, 23, 59, 59);
        using var intranet = Intranet();
        using var sd = Sd();
        SeedModuly(intranet);
        intranet.HotZaznamy.AddRange(Zaznam("100001", typ: "PMP"), Zaznam("100002", typ: "PNF"),
            Zaznam("100003", typ: "PMP", stav: "archiv"), Zaznam("100004", typ: "PMP", modul: "JINY"),
            Zaznam("100005", typ: "PMP"));
        intranet.HotKalkulace.AddRange(
            // dvě revize téhož ticketu → vyšší id (rozhodnutí uživatele 2026-09-08, stejně jako výzvy)
            new HotKalkulaceEntity { Id = 1, Pid = "100001", Akceptace = "Akceptováno", Cena = 1000, DatumAkceptace = new DateTime(2026, 2, 1) },
            new HotKalkulaceEntity { Id = 2, Pid = "100001", Akceptace = "Fakturováno", Cena = 1500, DatumAkceptace = new DateTime(2026, 3, 1) },
            // zákazový seznam (i s jinými mezerami) → vyřazeno
            new HotKalkulaceEntity { Id = 3, Pid = "100002", Akceptace = "Akceptovat?", Cena = 900, DatumAkceptace = new DateTime(2026, 4, 1) },
            new HotKalkulaceEntity { Id = 4, Pid = "100002", Akceptace = "Návrh", Cena = 900, DatumAkceptace = new DateTime(2026, 4, 1) },
            // archivovaný ticket IS s akceptací v období → patří tam
            new HotKalkulaceEntity { Id = 5, Pid = "100003", Akceptace = "Akceptováno s výhradou k ceně", Cena = 300, DatumAkceptace = new DateTime(2026, 5, 1) },
            // jiný IS → ne
            new HotKalkulaceEntity { Id = 6, Pid = "100004", Akceptace = "Akceptováno", Cena = 700, DatumAkceptace = new DateTime(2026, 5, 1) },
            // akceptace mimo období → ne
            new HotKalkulaceEntity { Id = 7, Pid = "100005", Akceptace = "Akceptováno", Cena = 200, DatumAkceptace = new DateTime(2025, 12, 31) });
        intranet.SaveChangesForTests();

        var kalk = await new SqlProdleniQueryService(intranet, sd).GetAkceptovaneKalkulaceAsync(IsId, od, @do, default);

        kalk.Should().BeEquivalentTo(new[]
        {
            new AkceptovanaKalkulaceDto(100001, new DateTime(2026, 3, 1), 1500),
            new AkceptovanaKalkulaceDto(100003, new DateTime(2026, 5, 1), 300),
        });
    }

    [Fact]
    public async Task Disabled_vraci_prazdne()
    {
        var svc = new DisabledProdleniQueryService();

        svc.Zapnuto.Should().BeFalse();
        (await svc.GetSynchronizovaneTicketyAsync(IsId, null, default)).Should().BeEmpty();
        (await svc.GetWorkflowAsync([1, 2], default)).Should().BeEmpty();
        (await svc.GetSlaKatalogAsync(default)).Should().BeEmpty();
        (await svc.GetAkceptovaneKalkulaceAsync(IsId, DateTime.MinValue, DateTime.MaxValue, default)).Should().BeEmpty();
    }

    private static IConfiguration Konfigurace(bool enabled, string? externi)
    {
        var data = new Dictionary<string, string?>
        {
            ["Ticketing:Enabled"] = enabled ? "true" : "false",
            ["ConnectionStrings:TicketingReadOnly"] = "Server=nikam;Database=intranetNEW;Integrated Security=True",
        };
        if (externi is not null) data["ConnectionStrings:ServiceDeskExternalReadOnly"] = externi;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "Server=nikam;Database=sd")]
    public void Bez_connection_stringu_registruje_disabled_query_service(bool enabled, string? externi)
    {
        var services = new ServiceCollection().AddServiceDeskIntegration(Konfigurace(enabled, externi));

        services.Single(d => d.ServiceType == typeof(IProdleniQueryService)).ImplementationType
            .Should().Be(typeof(DisabledProdleniQueryService));
    }

    [Fact]
    public void S_connection_stringem_registruje_sql_query_service()
    {
        var services = new ServiceCollection().AddServiceDeskIntegration(Konfigurace(true, "Server=nikam;Database=sd"));

        services.Single(d => d.ServiceType == typeof(IProdleniQueryService)).ImplementationType
            .Should().Be(typeof(SqlProdleniQueryService));
    }
}
