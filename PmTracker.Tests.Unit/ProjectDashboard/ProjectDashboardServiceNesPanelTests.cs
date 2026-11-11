using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>
/// <see cref="ProjectDashboardService.BuildNesPanelAsync"/> (spec 2026-10-15 §10): panel je tenká vrstva nad
/// <see cref="IProdleniService"/> — přebírá přehled a skládá z něj KPI řadu a dva žebříčkové grafy.
/// </summary>
public sealed class ProjectDashboardServiceNesPanelTests
{
    private const int ProjektId = 30;
    private static readonly DateTime Nyni = new(2026, 10, 20, 10, 0, 0);

    [Fact]
    public async Task NesPanel_mapuje_prehled_a_grafy()
    {
        var prehled = Prehled(
            vProdleni:
            [
                Polozka(900101, "NES", "ACME", 12),
                Polozka(900102, "PMP", "ACME", 5),
                Polozka(900103, "NES", "BETA", 3),
            ],
            hrozi: [new HroziPolozka(900104, "PNF", "Hrozi", "ACME", Nyni.AddDays(2), TimeSpan.FromDays(2))],
            nelze: [new NelzeVyhodnotitPolozka(900105, "NES", "Bez SLA", "X", PenaltyChyba.SlaChybiProDulezitost)]);

        var model = await Sestav(prehled);

        model.ProjektId.Should().Be(ProjektId);
        model.Nyni.Should().Be(Nyni);
        model.Prehled.Should().BeSameAs(prehled);

        model.Kpi.Should().NotBeNull();
        model.Kpi!.Kind.Should().Be(ChartKind.Kpi);
        model.Kpi.Stats.Select(s => s.Value).Should().Equal("3", "20", "1", "1");
        model.Kpi.Stats[0].Status.Should().Be(CardStatus.Critical);
        model.Kpi.Stats[2].Status.Should().Be(CardStatus.Warning);
        model.Kpi.Stats[3].Status.Should().Be(CardStatus.Warning);

        model.PodleTypu.Should().NotBeNull();
        model.PodleTypu!.Kind.Should().Be(ChartKind.HorizontalBar);
        model.PodleTypu.Categories.Should().Equal("NES", "PMP");
        model.PodleTypu.Series.Should().ContainSingle().Which.Values.Should().Equal(2, 1);
    }

    [Fact]
    public async Task NesPanel_graf_dodavatelu_serazeny_sestupne()
    {
        var prehled = Prehled(vProdleni:
        [
            Polozka(900101, "NES", "BETA", 3),
            Polozka(900102, "PMP", "ACME", 5),
            Polozka(900103, "NES", "ACME", 12),
            Polozka(900104, "PNF", null, 4),
        ]);

        var model = await Sestav(prehled);

        var graf = model.DnyPodleDodavatele!;
        graf.Kind.Should().Be(ChartKind.HorizontalBar);
        graf.Unit.Should().Be("dní");
        graf.Categories.Should().Equal("ACME", "(neuvedeno)", "BETA");
        graf.Series.Should().ContainSingle().Which.Values.Should().Equal(17, 4, 3);
    }

    [Fact]
    public async Task NesPanel_ticket_u_vice_dodavatelu_je_v_kpi_jednou()
    {
        var prehled = Prehled(vProdleni:
        [
            Polozka(900101, "NES", "ACME", 4),
            Polozka(900101, "NES", "BETA", 2),
        ]);

        var model = await Sestav(prehled);

        model.Kpi!.Stats[0].Value.Should().Be("1", "KPI počítá tickety, ne řádky dodavatelů");
        model.Kpi.Stats[1].Value.Should().Be("6");
        model.PodleTypu!.Series[0].Values.Should().Equal(1);
    }

    [Fact]
    public async Task NesPanel_bez_prodleni_ma_kpi_v_poradku_a_prazdne_grafy()
    {
        var model = await Sestav(Prehled());

        model.Kpi!.Stats[0].Value.Should().Be("0");
        model.Kpi.Stats[0].Status.Should().Be(CardStatus.Good);
        model.DnyPodleDodavatele!.IsEmpty.Should().BeTrue();
        model.PodleTypu!.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProdleniStavDat.Nedostupne)]
    [InlineData(ProdleniStavDat.BezNapojeni)]
    public async Task NesPanel_nedostupne_bez_grafu(ProdleniStavDat stav)
    {
        var model = await Sestav(ProdleniPrehled.Prazdny(stav, 1, "FIS"));

        model.Prehled.Stav.Should().Be(stav);
        model.Kpi.Should().BeNull();
        model.DnyPodleDodavatele.Should().BeNull();
        model.PodleTypu.Should().BeNull();
    }

    private static async Task<ProjectDashboardNesPanelViewModel> Sestav(ProdleniPrehled prehled)
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new PmTrackerDbContext(options);
        var prodleni = new Mock<IProdleniService>(MockBehavior.Strict);
        prodleni.Setup(p => p.GetPrehledAsync(ProjektId, It.IsAny<CancellationToken>())).ReturnsAsync(prehled);
        var service = new ProjectDashboardService(db, prodleni.Object);

        return await service.BuildNesPanelAsync(ProjektId, Nyni, CancellationToken.None);
    }

    private static ProdleniPrehled Prehled(
        IReadOnlyList<ProdleniPolozka>? vProdleni = null,
        IReadOnlyList<HroziPolozka>? hrozi = null,
        IReadOnlyList<NelzeVyhodnotitPolozka>? nelze = null)
        => new(ProdleniStavDat.Ok, 1, "FIS", vProdleni ?? [], hrozi ?? [], nelze ?? [], [], []);

    private static ProdleniPolozka Polozka(int id, string typ, string? dodavatel, int dny)
        => new(id, $"FIS-{id}", typ, "B", "Popis", dodavatel, false, Nyni.AddDays(-dny - 2), Nyni.AddDays(-dny), null,
            dny, [], typ != "PMP", [], []);
}
