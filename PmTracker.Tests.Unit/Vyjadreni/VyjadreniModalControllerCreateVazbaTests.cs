using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PmTracker.Web.Controllers;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Models.ViewModels.Vyjadreni;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk;
using IPmAuthorizationService = PmTracker.Web.Services.Security.IAuthorizationService;

namespace PmTracker.Tests.Unit.Vyjadreni;

/// <summary>
/// Testy pro wiring <see cref="VyjadreniModalController.CreateVazba"/>
/// → <see cref="IBindingRebalanceService"/>. Controller má zůstat thin
/// (auth → delegate → map), vlastní doménová logika (cascade, tx) patří service.
/// </summary>
public sealed class VyjadreniModalControllerCreateVazbaTests
{
    private const int OsobaId = 42;
    private const int ProjektId = 100;
    private const int ZaznamId = 500;
    private const int ExterniOdkazId = 9001;

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("create-vazba-" + Guid.NewGuid())
            .Options);

    private static async Task SeedOwnershipAsync(PmTrackerDbContext db)
    {
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = ZaznamId,
            ProjektId = ProjektId,
            SubsystemId = 1,
            KategorieId = 1,
            HarmonogramSablonaVerze = 1,
            Nazev = "ok"
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = ExterniOdkazId,
            ZaznamId = ZaznamId,
            Cislo = "123456"
        });
        await db.SaveChangesAsync();
    }

    private static VyjadreniModalController BuildSut(
        PmTrackerDbContext db,
        IBindingRebalanceService rebalance)
    {
        var builder = new Mock<IVyjadreniModalViewModelBuilder>();
        var harvest = new Mock<IVyjadreniHarvestService>();
        var authz = new Mock<IPmAuthorizationService>();
        authz.Setup(x => x.HasPermissionAsync(OsobaId, PermissionKeys.RecordsEdit, ProjektId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(x => x.OsobaId).Returns(OsobaId);

        var ctrl = new VyjadreniModalController(
            db, builder.Object, harvest.Object, rebalance, authz.Object, currentUser.Object,
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<VyjadreniModalController>.Instance,
            new Mock<IAuditWriteService>().Object);
        ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return ctrl;
    }

    [Fact]
    public async Task CreateVazba_DelegatesToRebalanceService_AndReturnsPrimaryPlusCascade()
    {
        await using var db = NewDb();
        await SeedOwnershipAsync(db);

        var krokKey = Guid.NewGuid();
        var cascadeKrokKey = Guid.NewGuid();

        var rebalance = new Mock<IBindingRebalanceService>();
        BindingRebalanceRequest? captured = null;
        rebalance
            .Setup(x => x.CreateBindingAsync(It.IsAny<BindingRebalanceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BindingRebalanceRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new BindingRebalanceResult(
                BindingRebalanceOutcome.Success,
                PrimaryVazbaId: 111,
                CascadeUpdates: new[]
                {
                    new BindingCascadeUpdate(
                        KrokKey: cascadeKrokKey,
                        KrokPoradi: 7,
                        NewVazbaId: 222,
                        NewHotVyjadreniId: 888L,
                        SupersededVazbaIds: new[] { 99 })
                }));

        var sut = BuildSut(db, rebalance.Object);
        var result = await sut.CreateVazba(new CreateVazbaRequest
        {
            ExterniOdkazId = ExterniOdkazId,
            ZaznamId = ZaznamId,
            KrokKey = krokKey,
            HotVyjadreniId = 300L,
            DatumVyjadreni = new DateTime(2026, 5, 1),
            ProjektId = ProjektId
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();

        captured.Should().NotBeNull();
        captured!.KrokKey.Should().Be(krokKey);
        captured.OsobaId.Should().Be(OsobaId);
        captured.ZaznamId.Should().Be(ZaznamId);
        captured.ExterniOdkazId.Should().Be(ExterniOdkazId);
    }

    [Fact]
    public async Task CreateVazba_ServiceReturnsInvalidKrokKey_MapsToBadRequest()
    {
        await using var db = NewDb();
        await SeedOwnershipAsync(db);

        var rebalance = new Mock<IBindingRebalanceService>();
        rebalance
            .Setup(x => x.CreateBindingAsync(It.IsAny<BindingRebalanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BindingRebalanceResult(
                BindingRebalanceOutcome.InvalidKrokKey,
                PrimaryVazbaId: null,
                CascadeUpdates: Array.Empty<BindingCascadeUpdate>()));

        var sut = BuildSut(db, rebalance.Object);
        var result = await sut.CreateVazba(new CreateVazbaRequest
        {
            ExterniOdkazId = ExterniOdkazId,
            ZaznamId = ZaznamId,
            KrokKey = Guid.NewGuid(),
            HotVyjadreniId = 1L,
            DatumVyjadreni = new DateTime(2026, 5, 1),
            ProjektId = ProjektId
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateVazba_ServiceReturnsExterniOdkazNotFound_MapsToNotFound()
    {
        await using var db = NewDb();
        await SeedOwnershipAsync(db);

        var rebalance = new Mock<IBindingRebalanceService>();
        rebalance
            .Setup(x => x.CreateBindingAsync(It.IsAny<BindingRebalanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BindingRebalanceResult(
                BindingRebalanceOutcome.ExterniOdkazNotFound,
                PrimaryVazbaId: null,
                CascadeUpdates: Array.Empty<BindingCascadeUpdate>()));

        var sut = BuildSut(db, rebalance.Object);
        var result = await sut.CreateVazba(new CreateVazbaRequest
        {
            ExterniOdkazId = ExterniOdkazId,
            ZaznamId = ZaznamId,
            KrokKey = Guid.NewGuid(),
            HotVyjadreniId = 1L,
            DatumVyjadreni = new DateTime(2026, 5, 1),
            ProjektId = ProjektId
        }, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
