using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Handlers;

namespace PmTracker.Tests.Unit.Handlers;

/// <summary>
/// Proof of vertical-slice pattern: <see cref="SaveProjectHandler"/> má jedinou veřejnou metodu,
/// vlastní DI graph (DbContext + IAuditWriteService), a je izolovaně testovatelný.
/// </summary>
public sealed class SaveProjectHandlerTests
{
    [Fact]
    public async Task HandleAsync_NoId_CreatesProject()
    {
        await using var db = CreateDb();
        db.CiselnikStavuProjektu.Add(new CiselnikStavuProjektuEntity { Id = 1, Kod = "RUN", Nazev = "Running" });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditWriteService>();
        var sut = new SaveProjectHandler(db, audit.Object, new TextNormalizer());
        var user = NewUser(osobaId: 42);

        var id = await sut.HandleAsync(
            new SaveProjectRequest(new SaveProjectCommand
            {
                Id = null,
                Nazev = "Nový projekt",
                Zkratka = "NP",
                Stav = "RUN",
                PouzivatIdentJednani = false,
                MistoPlneni = null,
                CisloRamcoveSmlouvy = null
            }),
            user,
            CancellationToken.None);

        id.Should().BeGreaterThan(0);
        var stored = await db.Projekty.FindAsync(id);
        stored.Should().NotBeNull();
        stored!.CelyNazev.Should().Be("Nový projekt");
        audit.Verify(x => x.Add(42, It.Is<AuditWriteEntry>(e => e.ActionType == AuditActionType.Create)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithId_UpdatesProject()
    {
        await using var db = CreateDb();
        db.CiselnikStavuProjektu.Add(new CiselnikStavuProjektuEntity { Id = 1, Kod = "RUN", Nazev = "Running" });
        db.Projekty.Add(new ProjektEntity { Id = 10, Zkratka = "OLD", CelyNazev = "Starý název", StavId = 1 });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditWriteService>();
        var sut = new SaveProjectHandler(db, audit.Object, new TextNormalizer());
        var user = NewUser(osobaId: 7);

        var id = await sut.HandleAsync(
            new SaveProjectRequest(new SaveProjectCommand
            {
                Id = 10,
                Nazev = "Nový název",
                Zkratka = "NEW",
                Stav = "RUN",
                PouzivatIdentJednani = true,
                MistoPlneni = "Praha",
                CisloRamcoveSmlouvy = "RS/2026/01"
            }),
            user,
            CancellationToken.None);

        id.Should().Be(10);
        var stored = await db.Projekty.FindAsync(10);
        stored!.CelyNazev.Should().Be("Nový název");
        stored.Zkratka.Should().Be("NEW");
        stored.PouzivatIdentJednani.Should().BeTrue();
        stored.MistoPlneni.Should().Be("Praha");
        audit.Verify(x => x.Add(7, It.Is<AuditWriteEntry>(e => e.ActionType == AuditActionType.Update)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnknownStav_Throws()
    {
        await using var db = CreateDb();
        var sut = new SaveProjectHandler(db, Mock.Of<IAuditWriteService>(), new TextNormalizer());

        var act = async () => await sut.HandleAsync(
            new SaveProjectRequest(new SaveProjectCommand
            {
                Id = null,
                Nazev = "X",
                Zkratka = "X",
                Stav = "NEEXISTUJE",
                PouzivatIdentJednani = false,
                MistoPlneni = null,
                CisloRamcoveSmlouvy = null
            }),
            NewUser(1),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*stav*NEEXISTUJE*");
    }

    private static PmTrackerDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new PmTrackerDbContext(opts);
    }

    private static CurrentUserContextViewModel NewUser(int osobaId)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = osobaId,
            Jmeno = "Test",
            Prijmeni = "User",
            DisplayName = "Test User",
            Email = "t@t.cz",
            OrganizacniCelek = "QA",
            OrganizacniCelekKod = "QA",
            IsSuperAdmin = false,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = Array.Empty<int>(),
            DeletedProjectIds = Array.Empty<int>(),
            Authorization = new PmTracker.Web.Services.Security.AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }
}
