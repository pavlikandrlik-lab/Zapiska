using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedules;

/// <summary>
/// Sdílený validátor chronologie harmonogramu — tentýž zdroj pravidel pro uložení záznamu
/// i pro odeslání návrhu. Ruční vstupy musí být neklesající (stejný den OK); prázdný vstup
/// (žádný plán / žádná ruční skutečnost) nesmí generovat žádnou chybu.
/// </summary>
public sealed class ScheduleChronologyValidatorTests
{
    private static SaveRecordHarmonogramValueCommand Plan(int poradi, DateTime plan)
        => new() { Poradi = poradi, PlanDatum = plan };

    private static List<RecordValidationIssue> Collect(SaveRecordCommand command)
    {
        var issues = new List<RecordValidationIssue>();
        ScheduleChronologyValidator.CollectIssues(command, issues);
        return issues;
    }

    [Fact]
    public void ChronologicalPlan_ProducesNoIssues()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramHodnoty =
            [
                Plan(1, new DateTime(2026, 4, 1)),
                Plan(3, new DateTime(2026, 4, 10)),
                Plan(5, new DateTime(2026, 4, 20))
            ]
        };

        Collect(command).Should().BeEmpty();
    }

    [Fact]
    public void EqualPlanDates_SameDay_AreAllowed()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramHodnoty =
            [
                Plan(1, new DateTime(2026, 4, 10)),
                Plan(3, new DateTime(2026, 4, 10))
            ]
        };

        Collect(command).Should().BeEmpty();
    }

    [Fact]
    public void EmptyScheduleAndActual_ProducesNoIssues()
    {
        // Návrh založení záznamu: skutečnost nelze vyplnit → prázdná kolekce nesmí dělat problém.
        Collect(new SaveRecordCommand()).Should().BeEmpty();
    }

    [Fact]
    public void DescendingPlan_ProducesPlanChronologyIssue()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramHodnoty =
            [
                Plan(1, new DateTime(2026, 4, 30)),
                Plan(3, new DateTime(2026, 4, 10))
            ]
        };

        var issues = Collect(command);
        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be("schedule_plan_chronology");
        issues[0].FieldKey.Should().Be("HarmonogramHodnoty[1].PlanDatum");
    }

    private static SaveRecordHarmonogramValueCommand Skut(int poradi, DateTime datum, DateTime? puvodni = null)
        => new() { Poradi = poradi, SkutecnostDatum = datum, SkutecnostDatumPuvodni = puvodni };

    [Fact]
    public void ZmenenaKlesajiciSkutecnost_RucnichKroku_ProducesActualChronologyIssue()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramHodnoty = [Skut(2, new DateTime(2026, 4, 20)), Skut(5, new DateTime(2026, 4, 5))]
        };
        var issues = Collect(command);
        issues.Should().ContainSingle();
        issues[0].Rule.Should().Be("schedule_actual_chronology");
        issues[0].FieldKey.Should().Be("HarmonogramHodnoty[1].SkutecnostDatum");
    }

    [Fact]
    public void AutoKrokyVAutomatickemRezimu_SeNekontroluji()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramRezim = "Auto",
            HarmonogramHodnoty = [Skut(3, new DateTime(2026, 4, 20)), Skut(4, new DateTime(2026, 4, 5))]
        };
        Collect(command).Should().BeEmpty("kroky 1/3/4/6/7/10 v automatickém režimu řídí automat");
    }

    /// <summary>Review Focus 3 — staré nechronologické hodnoty, na které uživatel nesáhl, uložení neblokují.</summary>
    [Fact]
    public void NedotceneNechronologickeHodnoty_NeblokujiUlozeni()
    {
        var a = new DateTime(2026, 4, 20);
        var b = new DateTime(2026, 4, 5);
        var command = new SaveRecordCommand
        {
            HarmonogramRezim = "Manual",
            HarmonogramHodnoty = [Skut(3, a, puvodni: a), Skut(4, b, puvodni: b)]
        };
        Collect(command).Should().BeEmpty();
    }

    [Fact]
    public void EnsureChronological_Throws_OnDescendingPlan()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramHodnoty =
            [
                Plan(1, new DateTime(2026, 4, 30)),
                Plan(3, new DateTime(2026, 4, 10))
            ]
        };

        var act = () => ScheduleChronologyValidator.EnsureChronological(command);
        act.Should().Throw<RecordValidationException>();
    }

    [Fact]
    public void EnsureChronological_DoesNotThrow_OnValidChronologicalPlan()
    {
        var command = new SaveRecordCommand
        {
            HarmonogramHodnoty =
            [
                Plan(1, new DateTime(2026, 4, 1)),
                Plan(3, new DateTime(2026, 4, 10))
            ]
        };

        var act = () => ScheduleChronologyValidator.EnsureChronological(command);
        act.Should().NotThrow();
    }
}
