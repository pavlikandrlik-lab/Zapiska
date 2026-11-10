using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.ProjectDashboard;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.ProjectDashboard;

/// <summary>
/// Řádek kroku v rozbaleném detailu problémového záznamu (dashboard projektu, záložka Záznamy).
/// Sloupec „Skutečný termín“ ukazuje jen skutečně vyplněnou skutečnost — u kroku bez ní se dřív
/// zobrazoval plán (konec plánu, ze kterého se skutečnost dopočítávala).
/// </summary>
public sealed class ProjectDashboardStepDetailTests
{
    private static readonly DateTime Zalozeni = new(2026, 7, 1);
    private static readonly DateTime Dnes = new(2026, 11, 10);

    private static IReadOnlyList<ProjectDashboardStepDetailViewModel> Detail(params (int Poradi, DateTime? Plan, DateTime? Skutecnost)[] vyplnene)
    {
        var kroky = HarmonogramKroky.Vse
            .Select(def =>
            {
                var hodnota = vyplnene.FirstOrDefault(x => x.Poradi == def.Poradi);
                return new ScheduleDateStep(def.Poradi, hodnota.Plan, hodnota.Skutecnost);
            })
            .ToList();
        var kategorie = DashboardRecordCategorizer.CategorizeRecord(
            DashboardScheduleSnapshotBuilder.Build(Zalozeni, kroky, Dnes),
            Dnes,
            DashboardRecordCategorizer.DefaultApproachingThresholdDays);
        return kategorie.ProblematicSteps
            .Select(ps => ProjectDashboardService.MapStepDetail(ps.Step, ps.StepCategory, Dnes))
            .ToList();
    }

    [Fact]
    public void Blizici_se_krok_bez_skutecnosti_nema_skutecny_termin()
    {
        var detail = Detail((1, new DateTime(2026, 11, 14), null));

        detail.Should().ContainSingle();
        detail[0].PlannedDate.Should().Be(new DateTime(2026, 11, 14));
        detail[0].ActualDate.Should().BeNull();
    }

    [Fact]
    public void Krok_cekajici_na_skutecnost_nema_skutecny_termin_a_ukaze_dny_od_planu()
    {
        var detail = Detail((1, new DateTime(2026, 10, 1), null));

        detail.Should().ContainSingle();
        detail[0].ActualDate.Should().BeNull();
        detail[0].DaysSincePlanExpired.Should().Be(40);
    }

    [Fact]
    public void Zpozdeny_krok_ukaze_skutecny_termin_a_odchylku()
    {
        var detail = Detail(
            (1, new DateTime(2026, 9, 1), new DateTime(2026, 9, 11)),
            (2, new DateTime(2026, 12, 1), null));

        detail.Should().ContainSingle();
        detail[0].ActualDate.Should().Be(new DateTime(2026, 9, 11));
        detail[0].OffsetDays.Should().Be(10);
    }
}
