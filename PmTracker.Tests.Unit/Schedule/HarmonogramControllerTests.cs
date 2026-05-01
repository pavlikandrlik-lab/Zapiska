using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Controllers;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.Security;
using Xunit;
using IPmAuthorizationService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Plán 4 Feature C Task 7 — HarmonogramController endpoint gates:
/// - ToggleRezim: NotFound pro neexistující řádek, Forbidden bez permission, Auto spouští sync
/// - SelectCandidate: create-if-missing flow (gap #3), Manual režim blokace
/// </summary>
public sealed class HarmonogramControllerTests
{
    private const int ZaznamId = 42;
    private const int ProjektId = 7;
    private const int SablonaVerze = 1;
    private const int K3DelayTypId = 103;
    private static readonly Guid K3Key = Guid.Parse("33333333-3333-3333-3333-000000000003");

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("ctrl-" + Guid.NewGuid())
            .Options);

    private static async Task<PmTrackerDbContext> SeedAsync(int? rowId = 1, SkutecnostRezimEnum rezim = SkutecnostRezimEnum.Auto)
    {
        var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = ZaznamId, ProjektId = ProjektId, KategorieId = 1, Nazev = "t",
            DatumZalozeni = new DateTime(2026, 1, 1), DatumUkonceni = new DateTime(2026, 6, 1),
            SubsystemId = 1, HarmonogramSablonaVerze = SablonaVerze
        });
        db.CiselnikHarmonogramTypu.Add(new HarmonogramTypEntity
        {
            Id = K3DelayTypId, Kod = "HS03_DELAY", Nazev = "K3 delay", Hodnota = 0,
            SablonaVerze = SablonaVerze, KrokKey = K3Key, KrokPoradi = 3, JeZpozdeni = true,
            BarvaHex = "#DC2626"
        });
        if (rowId.HasValue)
        {
            db.ZaznamHarmonogramHodnoty.Add(new ZaznamHarmonogramHodnotaEntity
            {
                Id = rowId.Value, ZaznamId = ZaznamId, TypId = K3DelayTypId, HodnotaInt = 5,
                UpdatedAt = DateTime.UtcNow,
                SkutecnostRezim = rezim,
                SkutecnostZdroj = SkutecnostZdrojEnum.Automat
            });
        }
        await db.SaveChangesAsync();
        return db;
    }

    private static HarmonogramController CreateSut(
        PmTrackerDbContext db,
        bool hasPermission = true,
        Mock<IHarmonogramSkutecnostSyncService>? syncMock = null)
    {
        var sync = syncMock ?? new Mock<IHarmonogramSkutecnostSyncService>();
        sync.Setup(s => s.SyncZaznamAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int zid, CancellationToken _) => new HarmonogramSyncResult(zid, 0, 0, 0, 0));

        var authz = new Mock<IPmAuthorizationService>();
        authz.Setup(a => a.HasPermissionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasPermission);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.OsobaId).Returns(1);

        var audit = new Mock<IAuditWriteService>();
        audit.Setup(a => a.WriteAsync(It.IsAny<int?>(), It.IsAny<AuditWriteEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var pendingLock = new Mock<PmTracker.Web.Services.Records.IPendingScheduleProposalLockEvaluator>();
        pendingLock.Setup(p => p.EvaluateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PmTracker.Web.Services.Records.PendingScheduleProposalLockState(false, null, null, false, false));

        var ctrl = new HarmonogramController(
            db,
            sync.Object,
            authz.Object,
            currentUser.Object,
            TimeProvider.System,
            audit.Object,
            NullLogger<HarmonogramController>.Instance,
            pendingLock.Object);

        // HttpContext pro TraceIdentifier / User fallback
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return ctrl;
    }

    [Fact]
    public async Task ToggleRezim_NullRequest_Vraci_BadRequest()
    {
        await using var db = await SeedAsync();
        var ctrl = CreateSut(db);

        var result = await ctrl.ToggleRezim(null!, CancellationToken.None);

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task ToggleRezim_NeexistujiciRow_Vraci_NotFound()
    {
        await using var db = await SeedAsync();
        var ctrl = CreateSut(db);

        var result = await ctrl.ToggleRezim(
            new HarmonogramController.ToggleRezimRequest(999, SkutecnostRezimEnum.Manual),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ToggleRezim_BezPravopravy_Vraci_Forbidden()
    {
        await using var db = await SeedAsync();
        var ctrl = CreateSut(db, hasPermission: false);

        var result = await ctrl.ToggleRezim(
            new HarmonogramController.ToggleRezimRequest(1, SkutecnostRezimEnum.Manual),
            CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ToggleRezim_StejnyRezim_Vraci_Ok_Changed_False()
    {
        await using var db = await SeedAsync(rezim: SkutecnostRezimEnum.Auto);
        var ctrl = CreateSut(db);

        var result = await ctrl.ToggleRezim(
            new HarmonogramController.ToggleRezimRequest(1, SkutecnostRezimEnum.Auto),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ToggleRezim_AutoToManual_FlagneManual_A_NespustiSync()
    {
        await using var db = await SeedAsync(rezim: SkutecnostRezimEnum.Auto);
        var syncMock = new Mock<IHarmonogramSkutecnostSyncService>();
        syncMock.Setup(s => s.SyncZaznamAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int zid, CancellationToken _) => new HarmonogramSyncResult(zid, 0, 0, 0, 0));
        var ctrl = CreateSut(db, syncMock: syncMock);

        var result = await ctrl.ToggleRezim(
            new HarmonogramController.ToggleRezimRequest(1, SkutecnostRezimEnum.Manual),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.SkutecnostRezim.Should().Be(SkutecnostRezimEnum.Manual);
        row.SkutecnostZdroj.Should().Be(SkutecnostZdrojEnum.Manual,
            "Auto→Manual překlopí Zdroj z Automat na Manual.");
        // Sync se NEvolá pro Manual přepnutí (user si data drží)
        syncMock.Verify(s => s.SyncZaznamAsync(ZaznamId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleRezim_ManualToAuto_Spusti_Sync()
    {
        await using var db = await SeedAsync(rezim: SkutecnostRezimEnum.Manual);
        var syncMock = new Mock<IHarmonogramSkutecnostSyncService>();
        syncMock.Setup(s => s.SyncZaznamAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int zid, CancellationToken _) => new HarmonogramSyncResult(zid, 0, 0, 0, 0));
        var ctrl = CreateSut(db, syncMock: syncMock);

        var result = await ctrl.ToggleRezim(
            new HarmonogramController.ToggleRezimRequest(1, SkutecnostRezimEnum.Auto),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        syncMock.Verify(s => s.SyncZaznamAsync(ZaznamId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectCandidate_RowExistuje_NastaviPreferred()
    {
        await using var db = await SeedAsync();
        var ctrl = CreateSut(db);

        var result = await ctrl.SelectCandidate(
            new HarmonogramController.SelectCandidateRequest(HodnotaId: 1, ExterniOdkazId: 500),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var row = await db.ZaznamHarmonogramHodnoty.AsNoTracking().SingleAsync(h => h.Id == 1);
        row.PreferredExterniOdkazId.Should().Be(500);
    }

    [Fact]
    public async Task SelectCandidate_ManualRezim_Vraci_BadRequest()
    {
        await using var db = await SeedAsync(rezim: SkutecnostRezimEnum.Manual);
        var ctrl = CreateSut(db);

        var result = await ctrl.SelectCandidate(
            new HarmonogramController.SelectCandidateRequest(HodnotaId: 1, ExterniOdkazId: 500),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SelectCandidate_CreateIfMissing_VytvoriRow_A_NastaviPreferred()
    {
        // Gap #3: row neexistuje → endpoint ho vytvoří z ZaznamId + KrokPoradi
        await using var db = await SeedAsync(rowId: null);
        var ctrl = CreateSut(db);

        var result = await ctrl.SelectCandidate(
            new HarmonogramController.SelectCandidateRequest(
                HodnotaId: 0, ExterniOdkazId: 500, ZaznamId: ZaznamId, KrokPoradi: 3),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var rows = await db.ZaznamHarmonogramHodnoty.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].TypId.Should().Be(K3DelayTypId);
        rows[0].PreferredExterniOdkazId.Should().Be(500);
        rows[0].SkutecnostRezim.Should().Be(SkutecnostRezimEnum.Auto);
    }

    [Fact]
    public async Task SelectCandidate_Chybejici_HodnotaId_I_ZaznamCoords_Vraci_BadRequest()
    {
        await using var db = await SeedAsync();
        var ctrl = CreateSut(db);

        var result = await ctrl.SelectCandidate(
            new HarmonogramController.SelectCandidateRequest(HodnotaId: 0, ExterniOdkazId: 500),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
