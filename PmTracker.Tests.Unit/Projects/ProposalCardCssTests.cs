using System.IO;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Projects;

public sealed class ProposalCardCssTests
{
    private static string Css()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return File.ReadAllText(
            Path.Combine(dir!.FullName, "PmTracker.Web", "wwwroot", "css", "site.css"));
    }

    [Fact]
    public void ProposalCard_HasBorderAndPadding()
    {
        var css = Css();
        css.Should().Contain(".proposal-card {",
            "každý návrh potřebuje vlastní sub-kartu s orámováním");
        css.Should().Contain("var(--gov-color-border)",
            "border barva musí používat gov token pro dark-mode kompatibilitu");
    }

    [Fact]
    public void ProposalList_HasGap()
    {
        Css().Should().Contain(".proposal-list {",
            "kontejner návrhů potřebuje gap mezi sub-kartami");
    }

    [Fact]
    public void ProposalCardHeader_IsFlexRow()
    {
        var css = Css();
        css.Should().Contain(".proposal-card-header {",
            "header návrhu (název + badge) musí být flex row");
        css.Should().Contain("justify-content: space-between",
            "badge stavu zarovnaný vpravo");
    }
}
