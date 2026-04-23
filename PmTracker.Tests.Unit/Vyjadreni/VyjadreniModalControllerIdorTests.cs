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
/// Review finding S-4: IDOR na /Vyjadreni/HarmonogramVazba/Create a /Delete.
/// Útočník s records.edit na projekt A posílá request s ProjektId = A, ale
/// ExterniOdkazId nebo VazbaId, který patří do projektu B. Autorizace musí
/// validovat, že target rozšíření patří do req.ProjektId.
/// </summary>
public sealed class VyjadreniModalControllerIdorTests
{
    private const int AttackerOsobaId = 42;
    private const int ProjektA = 100;
    private const int ProjektB = 200;

    private static PmTrackerDbContext NewDb() => new(
        new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase("idor-" + Guid.NewGuid())
            .Options);

    private static VyjadreniModalController BuildSut(PmTrackerDbContext db)
    {
        var builder = new Mock<IVyjadreniModalViewModelBuilder>();
        var harvest = new Mock<IVyjadreniHarvestService>();
        var rebalance = new Mock<IBindingRebalanceService>();
        rebalance
            .Setup(x => x.CreateBindingAsync(It.IsAny<BindingRebalanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BindingRebalanceResult(
                BindingRebalanceOutcome.Success,
                PrimaryVazbaId: 1,
                CascadeUpdates: Array.Empty<BindingCascadeUpdate>()));
        var authz = new Mock<IPmAuthorizationService>();
        // Útočník MÁ records.edit v projektu A (attempt to escalate).
        authz.Setup(x => x.HasPermissionAsync(AttackerOsobaId, PermissionKeys.RecordsEdit, ProjektA, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        // Nemá v projektu B.
        authz.Setup(x => x.HasPermissionAsync(AttackerOsobaId, PermissionKeys.RecordsEdit, ProjektB, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(x => x.OsobaId).Returns(AttackerOsobaId);

        var audit = new Mock<IAuditWriteService>();
        var ctrl = new VyjadreniModalController(
            db,
            builder.Object,
            harvest.Object,
            rebalance.Object,
            authz.Object,
            currentUser.Object,
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<VyjadreniModalController>.Instance,
            audit.Object);

        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return ctrl;
    }

    /// <summary>
    /// Útočník v projektu A se snaží vytvořit vazbu na externí odkaz projektu B.
    /// Expected: Forbid (nikoli Ok).
    /// </summary>
    [Fact]
    public async Task CreateVazba_AttackerTargetsCrossProjectExterniOdkaz_ReturnsForbid()
    {
        await using var db = NewDb();
        // Záznam+odkaz v projektu B
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 500, ProjektId = ProjektB, SubsystemId = 1, KategorieId = 1,
            HarmonogramSablonaVerze = 1, Nazev = "target"
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 9001, ZaznamId = 500, Cislo = "999999"
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(db);

        // Útočník tvrdí, že jeho ProjektId = A (kde má authz), a chce mutovat (500, 9001) z B.
        var result = await sut.CreateVazba(new CreateVazbaRequest
        {
            ExterniOdkazId = 9001,
            ZaznamId = 500,
            KrokKey = Guid.NewGuid(),
            HotVyjadreniId = 1L,
            DatumVyjadreni = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc),
            ProjektId = ProjektA
        }, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>(
            "záznam 500 patří do projektu B; autorizace pro projekt A nemá přístup k jiným projektům.");
    }

    /// <summary>
    /// Útočník v projektu A se snaží smazat vazbu, která patří do projektu B.
    /// </summary>
    [Fact]
    public async Task DeleteVazba_AttackerTargetsCrossProjectVazba_ReturnsForbid()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 500, ProjektId = ProjektB, SubsystemId = 1, KategorieId = 1,
            HarmonogramSablonaVerze = 1, Nazev = "target"
        });
        db.VyjadreniVazby.Add(new ZaznamHarmonogramVyjadreniVazbaEntity
        {
            Id = 7001,
            ZaznamId = 500,
            KrokKey = Guid.NewGuid(),
            ExterniOdkazId = 9001,
            HotVyjadreniId = 1L,
            DatumVyjadreni = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc),
            Source = (byte)VazbaSource.Auto,
            Stav = (byte)VazbaStav.Active,
            CreatedAt = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(db);

        var result = await sut.DeleteVazba(new DeleteVazbaRequest
        {
            VazbaId = 7001,
            ProjektId = ProjektA
        }, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>(
            "vazba 7001 patří k záznamu 500 v projektu B; útočník v projektu A nesmí mutovat.");

        var vazba = await db.VyjadreniVazby.FindAsync(7001);
        vazba!.Stav.Should().Be((byte)VazbaStav.Active, "vazba nesmí být soft-deleted");
    }

    /// <summary>
    /// Sanity check: legitimní request v projektu B (když by uživatel authz měl) projde — ověřuje,
    /// že IDOR fix neblokuje legální flow.
    /// </summary>
    [Fact]
    public async Task CreateVazba_LegitimateRequestSameProjekt_Passes()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 500, ProjektId = ProjektA, SubsystemId = 1, KategorieId = 1,
            HarmonogramSablonaVerze = 1, Nazev = "valid"
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 9001, ZaznamId = 500, Cislo = "999999"
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(db);

        var result = await sut.CreateVazba(new CreateVazbaRequest
        {
            ExterniOdkazId = 9001,
            ZaznamId = 500,
            KrokKey = Guid.NewGuid(),
            HotVyjadreniId = 1L,
            DatumVyjadreni = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc),
            ProjektId = ProjektA
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    /// <summary>
    /// Review finding M-R2-1: Modal (GET) — attacker volá s cizím externiOdkazId.
    /// Kontrola vlastnictví MUSÍ běžet PŘED _harvest.HarvestSingleTicketAsync,
    /// jinak DoS amplifier + cross-project LastHarvestedAt mutation.
    /// </summary>
    [Fact]
    public async Task Modal_AttackerTargetsCrossProjectExterniOdkaz_DoesNotTriggerHarvest()
    {
        await using var db = NewDb();
        // Záznam+odkaz v projektu B
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 500, ProjektId = ProjektB, SubsystemId = 1, KategorieId = 1,
            HarmonogramSablonaVerze = 1, Nazev = "target"
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 9001, ZaznamId = 500, Cislo = "999999"
        });
        // A pomocný záznam v projektu A, který útočník legitimně vlastní —
        // ale useruje ho jako ZaznamId, aby IDOR test zůstal symetrický s S-4.
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 501, ProjektId = ProjektA, SubsystemId = 1, KategorieId = 1,
            HarmonogramSablonaVerze = 1, Nazev = "attacker-record"
        });
        await db.SaveChangesAsync();

        var builder = new Mock<IVyjadreniModalViewModelBuilder>();
        var harvest = new Mock<IVyjadreniHarvestService>();
        var authz = new Mock<IPmAuthorizationService>();
        authz.Setup(x => x.HasPermissionAsync(AttackerOsobaId, PermissionKeys.RecordsEdit, It.IsAny<int>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, string __, int? projektId, int? ___, CancellationToken _____) => projektId == ProjektA);

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(x => x.OsobaId).Returns(AttackerOsobaId);

        var rebalance = new Mock<IBindingRebalanceService>();
        var ctrl = new VyjadreniModalController(
            db, builder.Object, harvest.Object, rebalance.Object, authz.Object, currentUser.Object,
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<VyjadreniModalController>.Instance,
            new Mock<IAuditWriteService>().Object);
        ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Attacker pošle externiOdkazId projektu B, ale zaznamId projektu A.
        // Ownership query join musí vrátit null (eo 9001 patří k záznamu 500, ne 501) → NotFound.
        var result = await ctrl.Modal(externiOdkazId: 9001, zaznamId: 501, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
        harvest.Verify(x => x.HarvestSingleTicketAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never,
            "harvest nesmí být spuštěn před ownership checkem (DoS amplifier).");
    }

    /// <summary>
    /// Review finding M-R2-1: Refresh (POST) — attacker volá s projektId = A,
    /// ale externiOdkazId patří projektu B. Harvest nesmí být spuštěn, odpověď Forbid.
    /// </summary>
    [Fact]
    public async Task Refresh_AttackerTargetsCrossProjectExterniOdkaz_DoesNotTriggerHarvest()
    {
        await using var db = NewDb();
        db.ProjektoveZaznamy.Add(new ProjektovyZaznamEntity
        {
            Id = 500, ProjektId = ProjektB, SubsystemId = 1, KategorieId = 1,
            HarmonogramSablonaVerze = 1, Nazev = "target"
        });
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity
        {
            Id = 9001, ZaznamId = 500, Cislo = "999999"
        });
        await db.SaveChangesAsync();

        var sut = BuildSut(db);
        // Injectujeme znovu, ať máme odkaz na harvest mock i mimo BuildSut:
        var harvest = new Mock<IVyjadreniHarvestService>();
        var authz = new Mock<IPmAuthorizationService>();
        authz.Setup(x => x.HasPermissionAsync(AttackerOsobaId, PermissionKeys.RecordsEdit, ProjektA, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(x => x.OsobaId).Returns(AttackerOsobaId);

        var rebalance = new Mock<IBindingRebalanceService>();
        var ctrl = new VyjadreniModalController(
            db, new Mock<IVyjadreniModalViewModelBuilder>().Object,
            harvest.Object, rebalance.Object, authz.Object, currentUser.Object,
            new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero)),
            NullLogger<VyjadreniModalController>.Instance,
            new Mock<IAuditWriteService>().Object);
        ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        // Útočník: externiOdkazId = 9001 (projekt B), ale tvrdí projektId = A.
        var result = await ctrl.Refresh(externiOdkazId: 9001, projektId: ProjektA, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        harvest.Verify(x => x.HarvestSingleTicketAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never,
            "harvest nesmí být spuštěn před ověřením, že eo skutečně patří do deklarovaného projektu.");
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
