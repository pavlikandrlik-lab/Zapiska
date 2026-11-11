using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.ServiceDesk.Prodleni;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>Časová osa obrátek v detailu ticketu (spec 2026-10-15 §10): polohy v % počítá server.</summary>
public sealed class ProdleniTimelineBuilderTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 8, 0, 0);
    private static readonly DateTime Nyni = new(2026, 10, 11, 8, 0, 0);

    [Fact]
    public void Uzavrena_a_otevrena_obratka_maji_polohy_v_procentech()
    {
        var polozka = Polozka(
        [
            new WorkflowUdalost(1, Start, 1, "ACME", null),
            new WorkflowUdalost(2, Start.AddDays(2), 3, "ACME", null),
            new WorkflowUdalost(3, Start.AddDays(5), 2, "ACME", null),
        ]);

        var osa = ProdleniTimelineBuilder.Sestav(polozka, Nyni);

        osa.OsaOd.Should().Be(Start);
        osa.OsaDo.Should().Be(Nyni);
        osa.DnesPct.Should().Be(100);
        osa.Obratky.Should().HaveCount(2);
        osa.Obratky[0].OdPct.Should().Be(0);
        osa.Obratky[0].DoPct.Should().Be(20);
        osa.Obratky[0].Otevrena.Should().BeFalse();
        osa.Obratky[1].OdPct.Should().Be(50);
        osa.Obratky[1].DoPct.Should().Be(100);
        osa.Obratky[1].Otevrena.Should().BeTrue();
    }

    [Fact]
    public void Prodlouzeni_za_dneskem_natahne_osu()
    {
        var polozka = Polozka(
        [
            new WorkflowUdalost(1, Start, 1, "ACME", null),
            new WorkflowUdalost(2, Start.AddDays(3), 11, "ACME", Start.AddDays(20)),
        ], termin: Start.AddDays(4), prodlouzenoDo: Start.AddDays(20));

        var osa = ProdleniTimelineBuilder.Sestav(polozka, Nyni);

        osa.OsaDo.Should().Be(Start.AddDays(20));
        osa.DnesPct.Should().Be(50);
        osa.Prodlouzeni.Should().ContainSingle().Which.Pct.Should().Be(100);
        osa.Termin!.Pct.Should().Be(20);
    }

    [Fact]
    public void Penalizovane_dny_se_slouci_do_souvislych_useku()
    {
        var polozka = Polozka([new WorkflowUdalost(1, Start, 1, "ACME", null)],
            penale: [Start.Date.AddDays(2), Start.Date.AddDays(3), Start.Date.AddDays(6)]);

        var osa = ProdleniTimelineBuilder.Sestav(polozka, Nyni);

        osa.Penale.Should().HaveCount(2);
        osa.Penale[0].Od.Should().Be(Start.Date.AddDays(2));
        osa.Penale[0].Do.Should().Be(Start.Date.AddDays(4));
        osa.Penale[1].Od.Should().Be(Start.Date.AddDays(6));
        osa.Penale.Should().OnlyContain(p => p.OdPct >= 0 && p.DoPct <= 100 && p.OdPct < p.DoPct);
    }

    [Fact]
    public void Obratka_jineho_dodavatele_je_oznacena()
    {
        var polozka = Polozka(
        [
            new WorkflowUdalost(1, Start, 1, "BETA", null),
            new WorkflowUdalost(2, Start.AddDays(1), 3, "BETA", null),
            new WorkflowUdalost(3, Start.AddDays(2), 1, "ACME", null),
        ]);

        var osa = ProdleniTimelineBuilder.Sestav(polozka, Nyni);

        osa.Obratky.Select(o => o.JinyDodavatel).Should().Equal(true, false);
    }

    [Fact]
    public void Bez_udalosti_osa_nespadne()
    {
        var osa = ProdleniTimelineBuilder.Sestav(Polozka([]), Nyni);

        osa.Obratky.Should().BeEmpty();
        osa.DnesPct.Should().BeInRange(0, 100);
    }

    private static ProdleniPolozka Polozka(IReadOnlyList<WorkflowUdalost> udalosti, DateTime? termin = null,
        DateTime? prodlouzenoDo = null, IReadOnlyList<DateTime>? penale = null)
        => new(900101, "FIS-900101", "PMP", null, "Popis", "ACME", false, Start, termin, prodlouzenoDo,
            penale?.Count ?? 0, penale ?? [], false, udalosti, []);
}
