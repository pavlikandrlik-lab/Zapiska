using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Schedules;
using PmTracker.Web.Services.Vyzvy;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>
/// Unit testy pro <see cref="ProjectDashboardService.BuildNesPanelAsync"/> — Plán 5 Sprint B Task 4.
/// Pokrývá 3 scénáře definované v plánu:
///   1. Projekt bez ServiceDeskInfoSystemId → IsServiceDeskIntegrated=false
///   2. Projekt s IS + prázdný seznam z query service → Integrated=true, Count=0
///   3. Projekt s IS + 3 prodlené → správné mapování + průměr dnů
/// </summary>
public sealed class ProjectDashboardServiceNesPanelTests
{
    private const int FisId = 1; // = SdInfoSystemy.FisId
    private static readonly DateTime Reference = new(2026, 4, 24, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BuildNesPanel_ProjektBezNapojeni_VraciIsServiceDeskIntegratedFalse()
    {
        await using var db = CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 10,
            Zkratka = "P10",
            CelyNazev = "Projekt bez IS",
            StavId = 1,
            ServiceDeskInfoSystemId = null
        });
        await db.SaveChangesAsync();

        var queryService = new Mock<IInformacniSystemQueryService>(MockBehavior.Strict);
        var service = CreateService(db, queryService.Object);

        var result = await service.BuildNesPanelAsync(10, Reference, CancellationToken.None);

        result.IsServiceDeskIntegrated.Should().BeFalse();
        result.Items.Should().BeEmpty();
        result.PocetVProdleni.Should().Be(0);
        queryService.Verify(
            q => q.GetProdleneAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "query service se bez napojení NESMÍ volat");
    }

    [Fact]
    public async Task BuildNesPanel_NeexistujiciProjekt_VraciIsServiceDeskIntegratedFalse()
    {
        await using var db = CreateDb();
        var queryService = new Mock<IInformacniSystemQueryService>(MockBehavior.Strict);
        var service = CreateService(db, queryService.Object);

        var result = await service.BuildNesPanelAsync(999, Reference, CancellationToken.None);

        result.IsServiceDeskIntegrated.Should().BeFalse();
        queryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BuildNesPanel_ProjektSNapojenim_PrazdnyListProdlenych_VraciIntegratedTrueBezPolozek()
    {
        await using var db = CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 20,
            Zkratka = "P20",
            CelyNazev = "Projekt s FIS",
            StavId = 1,
            ServiceDeskInfoSystemId = FisId
        });
        await db.SaveChangesAsync();

        var queryService = new Mock<IInformacniSystemQueryService>(MockBehavior.Strict);
        queryService
            .Setup(q => q.GetProdleneAsync(FisId, Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ProdlenyTicketDto>());

        var service = CreateService(db, queryService.Object);
        var result = await service.BuildNesPanelAsync(20, Reference, CancellationToken.None);

        result.IsServiceDeskIntegrated.Should().BeTrue();
        result.ServiceDeskInfoSystemId.Should().Be(FisId);
        result.IsZkratka.Should().Be("FIS");
        result.Items.Should().BeEmpty();
        result.PocetVProdleni.Should().Be(0);
        result.PrumerneProdleniDni.Should().Be(0.0);
    }

    [Fact]
    public async Task BuildNesPanel_TriProdlene_SpravneMapovaniAPrumer()
    {
        await using var db = CreateDb();
        db.Projekty.Add(new ProjektEntity
        {
            Id = 30,
            Zkratka = "P30",
            CelyNazev = "Projekt s prodlenymi",
            StavId = 1,
            ServiceDeskInfoSystemId = FisId
        });
        await db.SaveChangesAsync();

        var tickets = new List<ProdlenyTicketDto>
        {
            new(
                Id: 111111,
                Pid: "FIS-111111",
                TypZaznamu: "NES",
                Strucne: "Chyba A",
                Dulezitost: "Vysoka",
                Zavaznost: "Kriticka",
                Modul: "FIS-UCT",
                Dodavatel: "ACME",
                Stav: "aktivni",
                Termin: Reference.AddDays(-10),
                DniProdleni: 10),
            new(
                Id: 222222,
                Pid: "FIS-222222",
                TypZaznamu: "PMP",
                Strucne: "Uloha B",
                Dulezitost: null,
                Zavaznost: null,
                Modul: "FIS-MZD",
                Dodavatel: "ACME",
                Stav: "aktivni",
                Termin: Reference.AddDays(-5),
                DniProdleni: 5),
            new(
                Id: 333333,
                Pid: "FIS-333333",
                TypZaznamu: "PNF",
                Strucne: "Uloha C",
                Dulezitost: null,
                Zavaznost: null,
                Modul: "FIS-UCT",
                Dodavatel: "BETA",
                Stav: "aktivni",
                Termin: Reference.AddDays(-3),
                DniProdleni: 3)
        };

        var queryService = new Mock<IInformacniSystemQueryService>(MockBehavior.Strict);
        queryService
            .Setup(q => q.GetProdleneAsync(FisId, Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tickets);

        var service = CreateService(db, queryService.Object);
        var result = await service.BuildNesPanelAsync(30, Reference, CancellationToken.None);

        result.IsServiceDeskIntegrated.Should().BeTrue();
        result.ServiceDeskInfoSystemId.Should().Be(FisId);
        result.IsZkratka.Should().Be("FIS");
        result.PocetVProdleni.Should().Be(3);
        // (10 + 5 + 3) / 3 = 6.0
        result.PrumerneProdleniDni.Should().Be(6.0);

        result.Items.Should().HaveCount(3);
        var first = result.Items[0];
        first.TicketId.Should().Be(111111);
        first.Pid.Should().Be("FIS-111111");
        first.TypZaznamu.Should().Be("NES");
        first.Strucne.Should().Be("Chyba A");
        first.Dodavatel.Should().Be("ACME");
        first.DniProdleni.Should().Be(10);
        first.Stav.Should().Be("aktivni");
        first.ServiceDeskUrl
            .Should().Be("https://servicedesk.fis.acr/Hotline/Ticket/Details/111111");
    }

    private static PmTrackerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    private static ProjectDashboardService CreateService(
        PmTrackerDbContext db,
        IInformacniSystemQueryService queryService)
    {
        // BuildNesPanelAsync nevolá VyzvyPanelBuilder — inert stuby stačí.
        var vyzvaServiceMock = new Mock<IVyzvaService>();
        var vyzvyPanelBuilder = new VyzvyPanelBuilder(vyzvaServiceMock.Object, db);

        return new ProjectDashboardService(db, vyzvyPanelBuilder, queryService);
    }
}
