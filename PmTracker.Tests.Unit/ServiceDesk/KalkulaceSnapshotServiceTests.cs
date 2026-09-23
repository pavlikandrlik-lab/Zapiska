using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Unit.Vyzvy;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>Pravidla snímku skutečné ceny (spec 2026-09-10 A3 R6).</summary>
public sealed class KalkulaceSnapshotServiceTests
{
    private const string Cislo = "336865";
    private const string Pid = "A490P00E21MX";

    private sealed class FakeTicketing : ITicketingQueryService
    {
        public Dictionary<string, HotZaznamDto> Zaznamy { get; } = new();
        public Dictionary<string, HotKalkulaceDto> Kalkulace { get; } = new();

        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult(Zaznamy.GetValueOrDefault(cislo));

        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotZaznamDto>>(
                cisla.Where(Zaznamy.ContainsKey).ToDictionary(c => c, c => Zaznamy[c]));

        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
            => Task.FromResult(Kalkulace.GetValueOrDefault(cislo));

        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(
                cisla.Where(Kalkulace.ContainsKey).ToDictionary(p => p, p => Kalkulace[p]));
    }

    private static HotKalkulaceDto Kalkulace(long id, decimal cena) => new(
        id, Pid,
        PracnostAnalyza: 0m, SazbaAnalyza: 0m, CenaAnalyza: 0m,
        PracnostProgramovani: 0m, SazbaProgramovani: 0m, CenaProgramovani: 0m,
        PracnostTestovani: 0m, SazbaTestovani: 0m, CenaTestovani: 0m,
        PracnostImplementace: 0m, SazbaImplementace: 0m, CenaImplementace: 0m,
        CenaCelkem: cena,
        PocetLicenci: 1m, SazbaLicence: cena, CenaLicence: cena, RozpadLicence: null,
        TextTermin: null);

    private static async Task<(PmTrackerDbContext Db, int VazbaId)> SeedAsync(
        string typKod = "PNF", decimal? kalkulaceCena = null)
    {
        var db = VyzvaServiceTestHarness.CreateDb();
        await VyzvaServiceTestHarness.SeedPnfTypAsync(db);
        if (typKod != "PNF")
        {
            db.CiselnikTypuExternichOdkazu.Add(new CiselnikTypuExternichOdkazuEntity { Id = 99, Kod = typKod, Nazev = typKod });
            await db.SaveChangesAsync();
        }

        var typId = await db.CiselnikTypuExternichOdkazu.Where(t => t.Kod == typKod).Select(t => t.Id).SingleAsync();
        var vazba = new ZaznamExterniOdkazEntity
        {
            Id = 700, ZaznamId = 100, TypOdkazuId = typId, Cislo = Cislo,
            PredpokladanaCena = 12000m, KalkulaceCena = kalkulaceCena,
        };
        db.ZaznamExterniOdkazy.Add(vazba);
        await db.SaveChangesAsync();
        return (db, vazba.Id);
    }

    private static KalkulaceSnapshotService Sut(PmTrackerDbContext db, ITicketingQueryService ticketing)
        => new(db, ticketing,
            new FakeTimeProvider(new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero)),
            NullLogger<KalkulaceSnapshotService>.Instance);

    [Fact]
    public async Task Sync_AkceptovanaKalkulace_ZapiseCenuIdACas()
    {
        var (db, id) = await SeedAsync();
        using var _ = db;
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy[Cislo] = new HotZaznamDto(Cislo, "PNF", "s", "p", Pid: Pid);
        ticketing.Kalkulace[Pid] = Kalkulace(5414, 10340m);

        await Sut(db, ticketing).SyncAsync(new[] { id }, CancellationToken.None);

        var vazba = await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id);
        vazba.KalkulaceCena.Should().Be(10340m);
        vazba.KalkulaceId.Should().Be(5414);
        vazba.KalkulaceNacteno.Should().Be(new DateTime(2026, 9, 10, 8, 0, 0));
        vazba.PredpokladanaCena.Should().Be(12000m, "předpokládaná cena se nikdy nepřepisuje (R1)");
    }

    [Fact]
    public async Task Sync_TiketBezAkceptovaneKalkulace_SnimekVynuluje()
    {
        var (db, id) = await SeedAsync(kalkulaceCena: 500m);
        using var _ = db;
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy[Cislo] = new HotZaznamDto(Cislo, "PNF", "s", "p", Pid: Pid);

        await Sut(db, ticketing).SyncAsync(new[] { id }, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id))
            .KalkulaceCena.Should().BeNull("kalkulace mohla být odvolána");
    }

    [Fact]
    public async Task Sync_NenalezenyTiket_SnimekNemeni()
    {
        var (db, id) = await SeedAsync(kalkulaceCena: 500m);
        using var _ = db;

        await Sut(db, new FakeTicketing()).SyncAsync(new[] { id }, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id))
            .KalkulaceCena.Should().Be(500m, "vypnutý ServiceDesk nesmí smazat známou cenu");
    }

    [Fact]
    public async Task Sync_VazbaJinehoTypuNezPnf_SeNecte()
    {
        var (db, id) = await SeedAsync(typKod: "NES");
        using var _ = db;
        var ticketing = new FakeTicketing();
        ticketing.Zaznamy[Cislo] = new HotZaznamDto(Cislo, "NES", "s", "p", Pid: Pid);
        ticketing.Kalkulace[Pid] = Kalkulace(1, 999m);

        await Sut(db, ticketing).SyncAsync(new[] { id }, CancellationToken.None);

        (await db.ZaznamExterniOdkazy.AsNoTracking().SingleAsync(x => x.Id == id))
            .KalkulaceCena.Should().BeNull("snímek se týká jen PNF");
    }
}
