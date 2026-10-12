using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedules;

/// <summary>
/// Task 5 (2026-10-12): přepínač „Automatické vyplňování harmonogramu" musí platit i v návrhu —
/// jediné pravidlo pro editor záznamu, editor návrhu i detail návrhu (RecordEditorComposition,
/// RecordProposalPayloadMapper, RecordProposalService.Queries).
/// </summary>
public sealed class HarmonogramAutoFillRuleTests
{
    private const byte Auto = (byte)SkutecnostRezimEnum.Auto;
    private const byte Manual = (byte)SkutecnostRezimEnum.Manual;

    [Fact]
    public void BezKroku_JeAuto() => HarmonogramAutoFillRule.IsAutoFill([]).Should().BeTrue();

    [Fact]
    public void RucniJenKroky2_5_8_9_PrepinacNevypnou()
        => HarmonogramAutoFillRule.IsAutoFill([(2, Manual), (5, Manual), (1, Auto)]).Should().BeTrue();

    [Fact]
    public void RucniAutoEligibleKrok_JeManual()
        => HarmonogramAutoFillRule.IsAutoFill([(3, Manual), (1, Auto)]).Should().BeFalse();

    [Theory]
    [InlineData("Auto", "Auto")]
    [InlineData("manual", "Manual")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("cokoli", null)]
    public void Normalize(string? vstup, string? ocekavany) => HarmonogramAutoFillRule.Normalize(vstup).Should().Be(ocekavany);
}
