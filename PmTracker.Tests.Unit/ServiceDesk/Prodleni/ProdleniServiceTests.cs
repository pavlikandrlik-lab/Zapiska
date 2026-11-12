using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk.Prodleni;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk.Prodleni;

/// <summary>Skladba přehledu prodlení (spec 2026-10-15 §10, §13, §16 R6/R8). „Teď" = st 25.03.2026 12:00.</summary>
public sealed class ProdleniServiceTests
{
    private const int ProjektId = 10, IsId = 1;
    private static readonly DateTime Ted = new(2026, 3, 25, 12, 0, 0);

    private sealed class FakeQuery : IProdleniQueryService
    {
        public bool Zapnuto { get; set; } = true;
        public List<ProdleniTicketDto> Tickety { get; } = [];
        public Dictionary<int, List<WorkflowUdalostDto>> Workflow { get; } = [];
        public List<SlaKatalogDto> Sla { get; } = [new(1, "NES I", 8), new(2, "NES II", 16), new(3, "NES III", 40)];
        public int VolaniTicketu { get; private set; }
        public Exception? Vyhod { get; set; }
        public DateTime? PosledniAktivniOd { get; private set; }

        public Task<IReadOnlyList<ProdleniTicketDto>> GetSynchronizovaneTicketyAsync(int isId, DateTime? aktivniOd, CancellationToken ct)
        {
            VolaniTicketu++;
            PosledniAktivniOd = aktivniOd;
            if (Vyhod is not null) throw Vyhod;
            return Task.FromResult<IReadOnlyList<ProdleniTicketDto>>(Tickety);
        }

        public Task<IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>>> GetWorkflowAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<int, IReadOnlyList<WorkflowUdalostDto>>>(
                Workflow.Where(w => ids.Contains(w.Key)).ToDictionary(w => w.Key, w => (IReadOnlyList<WorkflowUdalostDto>)w.Value));

        public Task<IReadOnlyList<SlaKatalogDto>> GetSlaKatalogAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SlaKatalogDto>>(Sla);

        public Task<IReadOnlyList<AkceptovanaKalkulaceDto>> GetAkceptovaneKalkulaceAsync(int isId, DateTime od, DateTime @do, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AkceptovanaKalkulaceDto>>([]);
    }

    private sealed class ZachytavaciLogger : ILogger<ProdleniService>
    {
        public List<(LogLevel Uroven, string Text)> Zaznamy { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Zaznamy.Add((logLevel, formatter(state, exception)));
        public IEnumerable<string> Varovani => Zaznamy.Where(z => z.Uroven == LogLevel.Warning).Select(z => z.Text);
    }

    private readonly FakeQuery _query = new();
    private readonly ZachytavaciLogger _log = new();
    private int _dalsiWfId = 1;
    private readonly FakeTimeProvider _cas = NovyCas();

    private static FakeTimeProvider NovyCas()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(Ted, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        return time;
    }

#pragma warning disable CS0618 // MemoryCacheOptions.Clock je jediná cesta, jak cache posunout v čase testu.
    private sealed class HodinyCache(TimeProvider time) : Microsoft.Extensions.Internal.ISystemClock
    {
        public DateTimeOffset UtcNow => time.GetUtcNow();
    }
#pragma warning restore CS0618

    private ProdleniService Sluzba(int? isProjektu = IsId)
    {
        var db = new PmTrackerDbContext(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Projekty.Add(new ProjektEntity { Id = ProjektId, Zkratka = "P10", CelyNazev = "Projekt", StavId = 1, ServiceDeskInfoSystemId = isProjektu });
        db.SaveChanges();

        var time = _cas;
        return new ProdleniService(db, _query, new PenaltyCalculator(new CeskeSvatky(), time), time,
            new MemoryCache(new MemoryCacheOptions { Clock = new HodinyCache(time) }), _log);
    }

    private void Ticket(int id, string typ, string? dulezitost, params WorkflowUdalostDto[] udalosti)
    {
        var externi = 5000 + id % 1000;
        _query.Tickety.Add(new ProdleniTicketDto(id, "P" + id, typ, "Popis " + id, dulezitost, "R_EIS", "Dodavatel A", "dodavatel",
            null, externi, "TAG"));
        _query.Workflow[externi] = udalosti.ToList();
    }

    private WorkflowUdalostDto U(int typ, string datum, string? termin = null, string? dodavatel = null)
        => new(_dalsiWfId++, DateTime.Parse(datum), typ, dodavatel, termin is null ? null : DateTime.Parse(termin));

    [Fact]
    public async Task Bez_is_vraci_BezNapojeni()
    {
        var p = await Sluzba(isProjektu: null).GetPrehledAsync(ProjektId, default);

        p.Stav.Should().Be(ProdleniStavDat.BezNapojeni);
        p.IsId.Should().BeNull();
        _query.VolaniTicketu.Should().Be(0);
    }

    [Fact]
    public async Task Vypnuty_dotaz_vraci_BezNapojeni_s_is()
    {
        _query.Zapnuto = false;
        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.Stav.Should().Be(ProdleniStavDat.BezNapojeni);
        p.IsId.Should().Be(IsId);
        _query.VolaniTicketu.Should().Be(0);
    }

    [Fact]
    public async Task Rozdeli_na_vprodleni_hrozi_nelze()
    {
        Ticket(100001, "PMP", null, U(1, "2026-03-02 10:00", "2026-03-05 23:59:59"));         // 20 dní
        Ticket(100002, "NES", "NES II", U(1, "2026-03-23 08:00"));                             // 2 dny
        Ticket(100003, "NES", "NES III", U(1, "2026-03-25 10:00"));                            // deadline 27.03. 02:00 → zbývá 38 h
        Ticket(100004, "PMP", null, U(1, "2026-03-20 10:00", "2026-03-28 23:59:59"));         // zbývá 3,5 dne
        Ticket(100005, "PMP", null, U(2, "2026-03-20 10:00", "2026-03-28 23:59:59"));         // bez U1
        Ticket(100006, "PMP", null, U(1, "2026-03-20 10:00", "2026-04-05 23:59:59"));         // daleko → nikde

        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.Stav.Should().Be(ProdleniStavDat.Ok);
        p.IsZkratka.Should().Be("FIS");
        p.VProdleni.Select(x => (x.TicketId, x.PenalizovaneDny)).Should().Equal((100001, 20), (100002, 2));
        p.SoucetDni.Should().Be(22);
        p.Hrozi.Select(x => x.TicketId).Should().BeEquivalentTo([100003, 100004]);
        p.Hrozi.Single(x => x.TicketId == 100003).Zbyva.Should().Be(TimeSpan.FromHours(38));
        p.NelzeVyhodnotit.Should().ContainSingle().Which.Should().Match<NelzeVyhodnotitPolozka>(
            n => n.TicketId == 100005 && n.Chyba == PenaltyChyba.ChybiIniciacniUdalost);

        var nes = p.VProdleni.Single(x => x.TicketId == 100002);
        nes.PracovniDny.Should().BeTrue();
        nes.UDodavateleOd.Should().Be(new DateTime(2026, 3, 23, 8, 0, 0));
        nes.Termin.Should().Be(new DateTime(2026, 3, 24));
        nes.Duvody.Select(d => d.Kod).Should().Equal(PenaltyReasonKod.T02, PenaltyReasonKod.T05);
        p.VProdleni.Single(x => x.TicketId == 100001).Termin.Should().Be(new DateTime(2026, 3, 5, 23, 59, 59));
    }

    [Fact]
    public async Task Vraceny_ticket_s_penale_neni_v_prodleni()
    {
        Ticket(100001, "PMP", null, U(1, "2026-03-02 10:00", "2026-03-05 23:59:59"), U(3, "2026-03-09 14:00"));

        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.VProdleni.Should().BeEmpty();
        p.Penalizace.Should().ContainSingle().Which.Dny.Should().HaveCount(4);
        p.Otevrene.Should().ContainSingle().Which.UDodavatele.Should().BeFalse();
    }

    [Fact]
    public async Task Chyba_mimo_dodavatele_se_nezobrazi()
    {
        Ticket(100001, "PMP", null, U(3, "2026-03-09 14:00"));

        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.NelzeVyhodnotit.Should().BeEmpty();
        p.VProdleni.Should().BeEmpty();
    }

    [Fact]
    public async Task Vice_dodavatelu_dve_polozky()
    {
        // A měl ticket po termínu a vrátil ho; teď je u B a je po termínu. V prodlení je jen B (právě u něj ticket je),
        // penále obou dodavatelů jdou do statistik zvlášť.
        Ticket(100001, "PMP", null,
            U(1, "2026-03-02 10:00", "2026-03-03 23:59:59", dodavatel: "A"),
            U(3, "2026-03-06 10:00"),
            U(2, "2026-03-10 10:00", "2026-03-20 23:59:59", dodavatel: "B"));

        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.Penalizace.Select(x => (x.Dodavatel, x.Dny.Count)).Should().BeEquivalentTo([("A", 3), ("B", 5)]);
        var polozka = p.VProdleni.Should().ContainSingle().Subject;
        polozka.Dodavatel.Should().Be("B");
        polozka.ViceDodavatelu.Should().BeTrue();
        polozka.UDodavateleOd.Should().Be(new DateTime(2026, 3, 10, 10, 0, 0));
        polozka.Duvody[0].Kod.Should().Be(PenaltyReasonKod.T00);
    }

    [Fact]
    public async Task Razeni_podle_dni_sestupne()
    {
        Ticket(100001, "PMP", null, U(1, "2026-03-20 10:00", "2026-03-22 23:59:59")); // 3 dny
        Ticket(100002, "PMP", null, U(1, "2026-03-02 10:00", "2026-03-05 23:59:59")); // 20 dní
        Ticket(100003, "PMP", null, U(1, "2026-03-10 10:00", "2026-03-15 23:59:59")); // 10 dní

        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.VProdleni.Select(x => x.TicketId).Should().Equal(100002, 100003, 100001);
    }

    [Fact]
    public async Task Chybejici_sla_zaloguje_jednou()
    {
        Ticket(100001, "NES", "NES IV", U(1, "2026-03-23 08:00"));
        Ticket(100002, "NES", "NES IV", U(1, "2026-03-24 08:00"));

        var sluzba = Sluzba();
        var p = await sluzba.GetPrehledAsync(ProjektId, default);
        await sluzba.GetPrehledProObdobiAsync(ProjektId, new DateTime(2026, 1, 1), default); // jiný klíč přehledu, stejný katalog

        p.NelzeVyhodnotit.Select(n => n.Chyba).Should().Equal(PenaltyChyba.SlaChybiProDulezitost, PenaltyChyba.SlaChybiProDulezitost);
        _log.Varovani.Where(v => v.Contains("NES IV")).Should().HaveCount(1);
    }

    [Fact]
    public async Task Sla_parovani_orezava_mezery_na_obou_stranach()
    {
        _query.Sla.Clear();
        _query.Sla.Add(new SlaKatalogDto(1, " NES II ", 16));
        Ticket(100001, "NES", "NES II ", U(1, "2026-03-23 08:00")); // → 16 h → 2 dny
        Ticket(100002, "NES", "nes ii", U(1, "2026-03-23 08:00"));  // velikost písmen se rozlišuje → chyba

        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.VProdleni.Should().ContainSingle().Which.Should().Match<ProdleniPolozka>(x => x.TicketId == 100001 && x.PenalizovaneDny == 2);
        p.NelzeVyhodnotit.Should().ContainSingle().Which.TicketId.Should().Be(100002);
    }

    [Fact]
    public async Task Sla_kolize_po_orezu_bere_nizsi_id()
    {
        _query.Sla.Clear();
        _query.Sla.Add(new SlaKatalogDto(7, "NES I ", 40));
        _query.Sla.Add(new SlaKatalogDto(3, "NES I", 8));
        Ticket(100001, "NES", "NES I", U(1, "2026-03-23 08:00")); // 8 h → deadline 23.03. 16:00 → 23., 24., 25. = 3 dny

        var p = await Sluzba().GetPrehledAsync(ProjektId, default);

        p.VProdleni.Single().PenalizovaneDny.Should().Be(3);
        _log.Varovani.Should().Contain(v => v.Contains("NES I") && v.Contains('3') && v.Contains('7'));
    }

    [Fact]
    public async Task Druhe_volani_z_cache()
    {
        Ticket(100001, "PMP", null, U(1, "2026-03-02 10:00", "2026-03-05 23:59:59"));
        var sluzba = Sluzba();

        var prvni = await sluzba.GetPrehledAsync(ProjektId, default);
        var druhy = await sluzba.GetPrehledAsync(ProjektId, default);

        _query.VolaniTicketu.Should().Be(1);
        druhy.Should().BeSameAs(prvni);
    }

    [Fact]
    public async Task GetPrehled_pri_vyjimce_dotazu_vraci_stav_nedostupne()
    {
        _query.Vyhod = new InvalidOperationException("SQL timeout");
        var sluzba = Sluzba();

        var p = await sluzba.GetPrehledAsync(ProjektId, default);
        await sluzba.GetPrehledAsync(ProjektId, default);

        p.Stav.Should().Be(ProdleniStavDat.Nedostupne);
        p.IsId.Should().Be(IsId);
        _query.VolaniTicketu.Should().Be(1,
            "výpadek se krátce pamatuje — souhrn, panel a Statistiky by jinak každý čekaly na timeout SQL");
        _log.Varovani.Should().Contain(v => v.Contains(IsId.ToString()));
    }

    [Fact]
    public async Task Vypadek_se_po_minute_zkusi_znovu()
    {
        _query.Vyhod = new InvalidOperationException("SQL timeout");
        var sluzba = Sluzba();
        await sluzba.GetPrehledAsync(ProjektId, default);

        _query.Vyhod = null;
        _cas.Advance(TimeSpan.FromSeconds(61));
        var p = await sluzba.GetPrehledAsync(ProjektId, default);

        p.Stav.Should().Be(ProdleniStavDat.Ok, "po vypršení krátké paměti výpadku se databáze zkusí znovu");
        _query.VolaniTicketu.Should().Be(2);
    }

    [Fact]
    public async Task Obdobi_predava_aktivni_od_a_otevrene_jsou_bez_archivu()
    {
        Ticket(100001, "PMP", null, U(1, "2026-03-02 10:00", "2026-03-05 23:59:59"));
        _query.Tickety.Add(new ProdleniTicketDto(100002, "P2", "PNF", null, null, "R_EIS", "Dodavatel A", "archiv", null, 5002, null));
        _query.Workflow[5002] = [U(1, "2026-02-02 10:00", "2026-02-05 23:59:59"), U(3, "2026-02-10 10:00")];

        var p = await Sluzba().GetPrehledProObdobiAsync(ProjektId, new DateTime(2026, 1, 1), default);

        _query.PosledniAktivniOd.Should().Be(new DateTime(2026, 1, 1));
        p.Otevrene.Select(o => o.TicketId).Should().Equal(100001);
        p.Penalizace.Select(x => x.TicketId).Should().BeEquivalentTo([100001, 100002]);
    }

    [Fact]
    public async Task Archivovany_ticket_u_dodavatele_neni_v_prodleni_a_penale_konci_archivaci()
    {
        // Archivován bez převzetí (U5 po U1): obrátka zůstala „otevřená". Bez ořezu by penále rostlo každý den až do dneška.
        _query.Tickety.Add(new ProdleniTicketDto(100003, "P3", "PMP", null, null, "R_EIS", "Dodavatel A", "archiv", null, 5003, null));
        _query.Workflow[5003] = [U(1, "2026-02-02 10:00", "2026-02-05 23:59:59", "Dodavatel A"), U(5, "2026-02-20 09:00")];

        var p = await Sluzba().GetPrehledProObdobiAsync(ProjektId, new DateTime(2026, 1, 1), default);

        p.VProdleni.Should().BeEmpty("archivovaný ticket není u dodavatele (spec R3: archivované jen do statistik)");
        p.Hrozi.Should().BeEmpty();
        var dny = p.Penalizace.Single(x => x.TicketId == 100003).Dny;
        dny.Should().NotBeEmpty();
        dny.Max().Should().BeOnOrBefore(new DateTime(2026, 2, 20), "penále končí poslední událostí ticketu, ne dneškem");
        dny.Should().HaveCount(15, "PMP kalendářní dny 6.–20. 2.");
    }
}
