using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>
/// Problémové záznamy na dashboardu projektu hodnotí jen kroky s vlastním plánem. Krok bez plánu
/// nemá s čím srovnat: dopočtený konec plánu (konec předchozího plánu nebo datum založení) dával
/// stejné nesmysly jako stav harmonogramu před spec 2026-10-24.
/// </summary>
public sealed class DashboardScheduleSnapshotBuilderTests
{
    private static readonly DateTime Zalozeni = new(2026, 7, 1);
    private static readonly DateTime Dnes = new(2026, 10, 26);

    private static IReadOnlyList<ScheduleDateStep> Kroky(params (int Poradi, DateTime? Plan, DateTime? Skutecnost)[] vyplnene)
        => HarmonogramKroky.Vse
            .Select(def =>
            {
                var hodnota = vyplnene.FirstOrDefault(x => x.Poradi == def.Poradi);
                return new ScheduleDateStep(def.Poradi, hodnota.Plan, hodnota.Skutecnost);
            })
            .ToList();

    private static RecordCategorizationResult Kategorie(IReadOnlyList<ScheduleDateStep> kroky)
        => DashboardRecordCategorizer.CategorizeRecord(
            DashboardScheduleSnapshotBuilder.Build(Zalozeni, kroky, Dnes),
            Dnes,
            DashboardRecordCategorizer.DefaultApproachingThresholdDays);

    [Fact]
    public void Krok_se_skutecnosti_bez_planu_neni_zpozdeny()
    {
        // Automat doplnil skutečnost kroků 1, 3 a 4, plán nikdo nezadal.
        var kroky = Kroky(
            (1, null, new DateTime(2026, 7, 10)),
            (3, null, new DateTime(2026, 9, 20)),
            (4, null, new DateTime(2026, 10, 1)));

        Kategorie(kroky).ProblematicSteps.Should().BeEmpty();
    }

    [Fact]
    public void Kroky_bez_planu_i_skutecnosti_nehlasi_propadly_plan()
    {
        Kategorie(Kroky()).ProblematicSteps.Should().BeEmpty();
    }

    [Fact]
    public void Krok_bez_planu_za_krokem_s_planem_se_nesrovnava_s_planem_predchoziho()
    {
        var kroky = Kroky(
            (1, new DateTime(2026, 8, 1), new DateTime(2026, 8, 1)),
            (2, null, new DateTime(2026, 9, 20)));

        Kategorie(kroky).ProblematicSteps.Should().BeEmpty();
    }

    [Fact]
    public void Krok_splneny_v_den_planu_neceka_na_skutecnost()
    {
        var kroky = Kroky(
            (1, new DateTime(2026, 8, 1), new DateTime(2026, 8, 1)),
            (2, new DateTime(2026, 12, 1), null));

        Kategorie(kroky).ProblematicSteps.Should().BeEmpty();
    }

    [Fact]
    public void Krok_se_skutecnosti_po_planu_je_dal_zpozdeny()
    {
        var kroky = Kroky(
            (1, new DateTime(2026, 8, 1), new DateTime(2026, 8, 1)),
            (2, new DateTime(2026, 9, 1), new DateTime(2026, 9, 11)));

        var vysledek = Kategorie(kroky);

        vysledek.Category.Should().Be(DashboardRecordCategory.Delayed);
        vysledek.WorstOffsetDays.Should().Be(10);
        vysledek.ProblematicSteps.Should().ContainSingle(x => x.Step.StepIndex == 2);
    }

    [Fact]
    public void Aktualni_krok_s_propadlym_planem_dal_ceka_na_skutecnost()
    {
        var kroky = Kroky(
            (1, new DateTime(2026, 8, 1), new DateTime(2026, 8, 1)),
            (2, new DateTime(2026, 9, 1), null));

        var vysledek = Kategorie(kroky);

        vysledek.Category.Should().Be(DashboardRecordCategory.AwaitingActual);
        vysledek.WorstOffsetDays.Should().Be(55);
        vysledek.ProblematicSteps.Should().ContainSingle(x => x.Step.StepIndex == 2);
    }

    [Fact]
    public void Krok_s_blizicim_se_planem_se_dal_hlasi()
    {
        var kroky = Kroky((1, new DateTime(2026, 10, 30), null));

        var vysledek = Kategorie(kroky);

        vysledek.Category.Should().Be(DashboardRecordCategory.ApproachingDeadline);
        vysledek.ProblematicSteps.Should().ContainSingle(x => x.Step.StepIndex == 1);
    }
}
