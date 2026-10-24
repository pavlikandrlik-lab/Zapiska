using FluentAssertions;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>Spec 2026-10-24 R3, R4, R6, R7, R8: texty souhrnu harmonogramu a odchylky kroku.</summary>
public sealed class ScheduleStatusTextTests
{
    private const string Krok = "Aktuální krok: 5. vyporadani pripominek";

    [Theory]
    [InlineData(1, " · skluz +1 den")]
    [InlineData(3, " · skluz +3 dny")]
    [InlineData(4, " · skluz +4 dny")]
    [InlineData(5, " · skluz +5 dní")]
    [InlineData(22, " · skluz +22 dní")]
    [InlineData(-1, " · v předstihu 1 den")]
    [InlineData(-2, " · v předstihu 2 dny")]
    [InlineData(-7, " · v předstihu 7 dní")]
    [InlineData(0, " · dle plánu")]
    public void State_k_planu_aktualniho_kroku(int prekroceni, string konec)
        => ScheduleStatusText.State(HarmonogramStavVztah.AktualniKrok, "5. vyporadani pripominek", prekroceni)
            .Should().Be(Krok + konec);

    [Theory]
    [InlineData(-1, "zbývá 1 den")]
    [InlineData(-2, "zbývají 2 dny")]
    [InlineData(-4, "zbývají 4 dny")]
    [InlineData(-12, "zbývá 12 dní")]
    [InlineData(1, "skluz +1 den")]
    [InlineData(2, "skluz +2 dny")]
    [InlineData(10, "skluz +10 dní")]
    public void State_k_celkovemu_terminu(int prekroceni, string termin)
        => ScheduleStatusText.State(HarmonogramStavVztah.CelkovyTermin, "5. vyporadani pripominek", prekroceni)
            .Should().Be($"{Krok} · plán není zadán · celkový termín: {termin}");

    [Fact]
    public void State_k_celkovemu_terminu_v_den_terminu()
        => ScheduleStatusText.State(HarmonogramStavVztah.CelkovyTermin, "5. vyporadani pripominek", 0)
            .Should().Be($"{Krok} · plán není zadán · celkový termín je dnes");

    [Fact]
    public void State_dokonceno()
        => ScheduleStatusText.State(HarmonogramStavVztah.Dokonceno, null, 0).Should().Be("Dokončeno");

    [Fact]
    public void PlanCompletion_bez_planu_neni_zadan()
        => ScheduleStatusText.PlanCompletion(null).Should().Be("není zadán");

    [Fact]
    public void PlanCompletion_s_planem_je_datum()
        => ScheduleStatusText.PlanCompletion(new DateTime(2026, 2, 4)).Should().Be("04.02.2026");

    [Theory]
    [InlineData(1, "1 den")]
    [InlineData(3, "3 dny")]
    [InlineData(304, "304 dnů")]
    [InlineData(0, "0 dnů")]
    public void TotalDuration_s_planem_sklonuje(int dni, string text)
        => ScheduleStatusText.TotalDuration(new DateTime(2026, 2, 4), dni).Should().Be(text);

    [Fact]
    public void TotalDuration_bez_planu_je_pomlcka()
        => ScheduleStatusText.TotalDuration(null, 0).Should().Be("–");

    [Theory]
    [InlineData(1, "+1 den")]
    [InlineData(3, "+3 dny")]
    [InlineData(5, "+5 dnů")]
    [InlineData(-2, "-2 dny")]
    [InlineData(-5, "-5 dnů")]
    [InlineData(0, "0 dnů")]
    public void StepOffset_krok_s_planem_sklonuje(int odchylka, string text)
        => ScheduleStatusText.StepOffset(new DateTime(2026, 2, 4), odchylka).Should().Be(text);

    [Fact]
    public void StepOffset_krok_bez_planu()
        => ScheduleStatusText.StepOffset(null, 37).Should().Be("bez plánu");
}
