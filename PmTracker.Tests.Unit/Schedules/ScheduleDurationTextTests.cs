using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedules;

/// <summary>Spec 2026-10-22 R13: text trvání kroku. Zrcadlo v JS: harmonogram/planDuration.js.</summary>
public sealed class ScheduleDurationTextTests
{
    [Theory]
    [InlineData(0, "0 dnů")]
    [InlineData(1, "1 den")]
    [InlineData(2, "2 dny")]
    [InlineData(4, "4 dny")]
    [InlineData(5, "5 dnů")]
    [InlineData(21, "21 dnů")]
    public void Format_CzechPlural(int days, string expected)
        => ScheduleDurationText.Format(days).Should().Be(expected);

    [Fact]
    public void ForStep_WithoutPlan_IsDash()
        => ScheduleDurationText.ForStep(planDatum: null, trvaniDni: 0).Should().Be("–");

    [Fact]
    public void ForStep_WithPlan_FormatsDays()
        => ScheduleDurationText.ForStep(new DateTime(2026, 10, 22), 3).Should().Be("3 dny");
}
