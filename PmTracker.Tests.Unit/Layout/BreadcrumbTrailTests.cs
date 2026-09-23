using FluentAssertions;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Unit.Layout;

public sealed class BreadcrumbTrailTests
{
    [Fact]
    public void ParentUrl_IsSecondToLastItemUrl()
    {
        var trail = new BreadcrumbTrail(new[]
        {
            new Breadcrumb("Projekty", "/Projekty", null, false),
            new Breadcrumb("Alfa", "/Projekty/Detail/7", "ALF", true),
            new Breadcrumb("Jednání 12", null, null, true),
        });

        trail.ParentUrl.Should().Be("/Projekty/Detail/7");
    }

    [Fact]
    public void ParentUrl_IsNull_ForSingleRoot()
    {
        var trail = new BreadcrumbTrail(new[] { new Breadcrumb("Projekty", null, null, false) });
        trail.ParentUrl.Should().BeNull();
    }

    /// <summary>C1 (2026-07-10): ← preferuje explicitní BackUrl (origin/kanonická záložka).</summary>
    [Fact]
    public void ParentUrl_PrefersExplicitBackUrl()
    {
        var trail = new BreadcrumbTrail(new[]
        {
            new Breadcrumb("Projekty", "/Projekty", null, false),
            new Breadcrumb("Alfa", "/Projekty/Detail/7", "ALF", true),
            new Breadcrumb("Jednání 12", null, null, true),
        }, "/Projekty/Detail/7?tab=jednani");

        trail.ParentUrl.Should().Be("/Projekty/Detail/7?tab=jednani");
    }

    [Fact]
    public void ParentUrl_FallsBackToSecondToLastItem_WhenBackUrlNull()
    {
        var trail = new BreadcrumbTrail(new[]
        {
            new Breadcrumb("Projekty", "/Projekty", null, false),
            new Breadcrumb("X", null, null, true),
        }, BackUrl: null);

        trail.ParentUrl.Should().Be("/Projekty");
    }

    [Fact]
    public void Current_IsLastItem_WithNullUrl()
    {
        var current = new Breadcrumb("Jednání 12", null, null, true);
        var trail = new BreadcrumbTrail(new[]
        {
            new Breadcrumb("Projekty", "/Projekty", null, false),
            current,
        });

        trail.Items[^1].Should().Be(current);
        trail.Items[^1].Url.Should().BeNull();
    }
}
