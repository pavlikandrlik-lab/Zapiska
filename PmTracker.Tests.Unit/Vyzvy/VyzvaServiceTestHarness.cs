using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.Vyzvy;

namespace PmTracker.Tests.Unit.Vyzvy;

internal static class VyzvaServiceTestHarness
{
    public static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }

    public static VyzvaService CreateService(
        PmTrackerDbContext db,
        ITicketingQueryService? ticketing = null,
        IAuthorizationService? authz = null,
        ICurrentUserAccessor? user = null)
    {
        // Provide no-op defaults so existing tests continue to pass.
        // Tests that don't care about authz get a mock that always allows.
        if (authz == null)
        {
            var authzMock = new Mock<IAuthorizationService>();
            authzMock
                .Setup(a => a.HasPermissionAsync(
                    It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            authz = authzMock.Object;
        }

        if (user == null)
        {
            var userMock = new Mock<ICurrentUserAccessor>();
            userMock.Setup(u => u.OsobaId).Returns(1);
            user = userMock.Object;
        }

        return new(db, ticketing ?? new StubTicketingQueryService(), authz, user);
    }

    public static async Task SeedProjektAsync(PmTrackerDbContext db, int projektId = 1)
    {
        if (await db.Projekty.AnyAsync(p => p.Id == projektId)) return;
        db.Projekty.Add(new ProjektEntity
        {
            Id = projektId,
            Zkratka = "P1", CelyNazev = "Projekt 1", StavId = 1,
            MistoPlneni = "FIS (EIS): VZ 8201",
            CisloRamcoveSmlouvy = "23106000271",
        });
        await db.SaveChangesAsync();
    }

    public static async Task SeedPnfTypAsync(PmTrackerDbContext db)
    {
        if (await db.CiselnikTypuExternichOdkazu.AnyAsync(t => t.Kod == "PNF")) return;
        db.CiselnikTypuExternichOdkazu.Add(new CiselnikTypuExternichOdkazuEntity
        {
            Id = 1, Kod = "PNF", Nazev = "PNF",
        });
        await db.SaveChangesAsync();
    }

    public static async Task SeedZaznamAsync(PmTrackerDbContext db, int zaznamId, int projektId = 1)
    {
        if (await db.ProjektoveZaznamy.AnyAsync(z => z.Id == zaznamId)) return;
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = zaznamId, ProjektId = projektId, KategorieId = 1, CisloZaznamu = zaznamId,
            CisloViditelne = $"RU{zaznamId}", Nazev = "test", VlastnikId = 1,
            DatumZalozeni = DateTime.UtcNow, DatumUkonceni = DateTime.UtcNow,
            SubsystemId = 1,
        });
        await db.SaveChangesAsync();
    }

    private sealed class StubTicketingQueryService : ITicketingQueryService
    {
        public Task<HotZaznamDto?> GetZaznamAsync(string cislo, CancellationToken ct)
            => Task.FromResult<HotZaznamDto?>(null);
        public Task<IReadOnlyDictionary<string, HotZaznamDto>> GetZaznamyAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotZaznamDto>>(new Dictionary<string, HotZaznamDto>());
        public Task<HotKalkulaceDto?> GetAkceptovanouKalkulaciAsync(string cislo, CancellationToken ct)
            => Task.FromResult<HotKalkulaceDto?>(null);
        public Task<IReadOnlyDictionary<string, HotKalkulaceDto>> GetAkceptovaneKalkulaceAsync(
            IReadOnlyCollection<string> cisla, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, HotKalkulaceDto>>(new Dictionary<string, HotKalkulaceDto>());
    }
}
