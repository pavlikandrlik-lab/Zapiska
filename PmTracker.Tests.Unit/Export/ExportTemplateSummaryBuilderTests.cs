using FluentAssertions;
using PmTracker.Web.Services.Export;

namespace PmTracker.Tests.Unit.Export;

public sealed class ExportTemplateSummaryBuilderTests
{
    [Fact]
    public void BuildProjectSummary_ShouldReturnProjectDefaults()
    {
        var sut = new ExportTemplateSummaryBuilder();

        var result = sut.BuildProjectSummary();

        result.JednaniStav.Should().Be("Projekt");
        result.SnapshotSummary.Should().Be("Tisk kompletního projektu bez filtru.");
        result.PreparationSummary.Should().BeNull();
        result.AppliedRuleSummary.Should().BeEquivalentTo(["Bez omezení"]);
    }

    [Fact]
    public void BuildMeetingSummary_ShouldUseFallbackStatus_WhenStatusIsEmpty()
    {
        var sut = new ExportTemplateSummaryBuilder();

        var result = sut.BuildMeetingSummary("  ");

        result.JednaniStav.Should().Be("-");
        result.AppliedRuleSummary.Should().BeEquivalentTo(["Automatický meeting výstup"]);
    }

    [Fact]
    public void BuildTaskSummary_ShouldReturnTaskDefaults()
    {
        var sut = new ExportTemplateSummaryBuilder();

        var result = sut.BuildTaskSummary();

        result.JednaniStav.Should().Be("Úkol");
        result.SnapshotSummary.Should().Be("Tisk jednoho úkolu.");
        result.AppliedRuleSummary.Should().BeEquivalentTo(["Automatický task výstup"]);
    }
}
