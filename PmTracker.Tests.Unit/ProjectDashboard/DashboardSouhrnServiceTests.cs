using FluentAssertions;
using Moq;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Security;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>Souhrnný pás dashboardu (spec 2026-10-15 §8): čtyři dlaždice podle oprávnění a napojení na IS.</summary>
public sealed class DashboardSouhrnServiceTests
{
    private const int ProjektId = 10;
    private static readonly DateTime Ted = new(2026, 3, 25, 12, 0, 0);

    private static CurrentUserContextViewModel Uzivatel(params string[] klice) => new()
    {
        OsobaId = 1,
        Jmeno = "Test",
        Prijmeni = "Uživatel",
        DisplayName = "Test Uživatel",
        Email = "t@example.cz",
        OrganizacniCelek = "OC",
        RoleKody = ["USER"],
        VisibleProjectIds = [ProjektId],
        DeletedProjectIds = [],
        Authorization = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>> { [ProjektId] = new HashSet<string>(klice) },
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
    };

    private static ProjectDashboardRecordRowViewModel Radek(DashboardRecordCategory kategorie) => new()
    {
        CisloViditelne = "1", Nazev = "N", Subsystem = "S", SubsystemKod = "S", Vlastnik = "V",
        Category = kategorie, ScheduleUrl = "/"
    };

    private static ProdleniPolozka Polozka(int ticketId, int dni, string dodavatel = "A") => new(
        ticketId, "P", "NES", "NES II", "x", dodavatel, false, Ted.AddDays(-3), null, null, dni, [], true, [], []);

    private readonly Mock<IProjectDashboardService> _dashboard = new(MockBehavior.Strict);
    private readonly Mock<IProdleniService> _prodleni = new(MockBehavior.Strict);

    private DashboardSouhrnService Sluzba() => new(_dashboard.Object, _prodleni.Object);

    private void Zaznamy(params DashboardRecordCategory[] kategorie)
        => _dashboard.Setup(d => d.BuildRecordsPanelAsync(ProjektId, Ted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectDashboardRecordsPanelViewModel { Records = kategorie.Select(Radek).ToList() });

    private void Prehled(ProdleniPrehled p)
        => _prodleni.Setup(s => s.GetPrehledAsync(ProjektId, It.IsAny<CancellationToken>())).ReturnsAsync(p);

    [Fact]
    public async Task Souhrn_bez_nes_opravneni_nema_ticketove_dlazdice()
    {
        Zaznamy(DashboardRecordCategory.Delayed);

        var vm = await Sluzba().BuildAsync(ProjektId, Uzivatel(PermissionKeys.DashboardView, PermissionKeys.DashboardRecordsView), Ted, default);

        vm.Dlazdice.Select(d => d.Klic).Should().Equal("ukoly-po-terminu", "ceka-na-skutecnost");
        _prodleni.Verify(s => s.GetPrehledAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Souhrn_bez_records_opravneni_nema_ukolove_dlazdice()
    {
        Prehled(ProdleniPrehled.Prazdny(ProdleniStavDat.Ok, 1, "FIS"));

        var vm = await Sluzba().BuildAsync(ProjektId, Uzivatel(PermissionKeys.DashboardView, PermissionKeys.DashboardNesView), Ted, default);

        vm.Dlazdice.Select(d => d.Klic).Should().Equal("tickety-v-prodleni", "hrozi-prodleni");
    }

    [Fact]
    public async Task Souhrn_bez_is_nema_ticketove_dlazdice()
    {
        Zaznamy();
        Prehled(ProdleniPrehled.Prazdny(ProdleniStavDat.BezNapojeni, null, null));

        var vm = await Sluzba().BuildAsync(ProjektId,
            Uzivatel(PermissionKeys.DashboardView, PermissionKeys.DashboardRecordsView, PermissionKeys.DashboardNesView), Ted, default);

        vm.Dlazdice.Select(d => d.Klic).Should().Equal("ukoly-po-terminu", "ceka-na-skutecnost");
    }

    [Fact]
    public async Task Souhrn_nula_je_Ok_stav()
    {
        Zaznamy(DashboardRecordCategory.ApproachingDeadline);
        Prehled(ProdleniPrehled.Prazdny(ProdleniStavDat.Ok, 1, "FIS"));

        var vm = await Sluzba().BuildAsync(ProjektId,
            Uzivatel(PermissionKeys.DashboardView, PermissionKeys.DashboardRecordsView, PermissionKeys.DashboardNesView), Ted, default);

        vm.Dlazdice.Should().HaveCount(4).And.OnlyContain(d => d.Hodnota == 0 && d.Stav == SouhrnStav.Ok);
    }

    [Fact]
    public async Task Souhrn_pocty_stavy_a_cile()
    {
        Zaznamy(DashboardRecordCategory.Delayed, DashboardRecordCategory.Delayed, DashboardRecordCategory.AwaitingActual,
            DashboardRecordCategory.ApproachingDeadline);
        Prehled(ProdleniPrehled.Prazdny(ProdleniStavDat.Ok, 1, "FIS") with
        {
            // jeden ticket u dvou dodavatelů se počítá jednou
            VProdleni = [Polozka(100001, 20), Polozka(100002, 15, "A"), Polozka(100002, 3, "B")],
            Hrozi = [new HroziPolozka(100003, "PMP", null, "A", Ted.AddDays(2), TimeSpan.FromDays(2))],
        });

        var vm = await Sluzba().BuildAsync(ProjektId,
            Uzivatel(PermissionKeys.DashboardView, PermissionKeys.DashboardRecordsView, PermissionKeys.DashboardNesView), Ted, default);

        vm.Dlazdice.Should().BeEquivalentTo(new[]
        {
            new SouhrnDlazdice("ukoly-po-terminu", 2, "Úkoly po termínu", null, SouhrnStav.Kriticke, "zaznamy", "delayed"),
            new SouhrnDlazdice("ceka-na-skutecnost", 1, "Čeká na skutečnost", null, SouhrnStav.Varovani, "zaznamy", "awaiting"),
            new SouhrnDlazdice("tickety-v-prodleni", 2, "Tickety v prodlení", "38 dní", SouhrnStav.Kriticke, "nes", "v-prodleni"),
            new SouhrnDlazdice("hrozi-prodleni", 1, "Hrozí prodlení", null, SouhrnStav.Varovani, "nes", "hrozi"),
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Souhrn_nedostupny_sd_dava_Nedostupne_a_null_hodnotu()
    {
        Prehled(ProdleniPrehled.Prazdny(ProdleniStavDat.Nedostupne, 1, "FIS"));

        var vm = await Sluzba().BuildAsync(ProjektId, Uzivatel(PermissionKeys.DashboardView, PermissionKeys.DashboardNesView), Ted, default);

        vm.Dlazdice.Should().HaveCount(2).And.OnlyContain(d => d.Hodnota == null && d.Stav == SouhrnStav.Nedostupne);
    }
}
