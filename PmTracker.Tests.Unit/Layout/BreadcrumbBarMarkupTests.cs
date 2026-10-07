using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

public sealed class BreadcrumbBarMarkupTests
{
    [Fact]
    public void Partial_RendersNavList_WithBackCloseAndCurrentHooks()
    {
        var cshtml = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml"));
        cshtml.Should().Contain("app-breadcrumb-bar");
        cshtml.Should().Contain("aria-label=\"Drobečková navigace\"");
        cshtml.Should().Contain("<ol");
        cshtml.Should().Contain("app-breadcrumb-back");
        cshtml.Should().Contain("app-breadcrumb-close");
        cshtml.Should().Contain("aria-current=\"page\"");
        cshtml.Should().Contain("name=\"arrow-left\"");
        cshtml.Should().Contain("name=\"x\"");
    }

    [Fact]
    public void Layout_RendersBreadcrumbBar_WhenTrailPresent()
    {
        var layout = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_Layout.cshtml"));
        layout.Should().Contain("ViewData[\"Breadcrumbs\"]");
        layout.Should().Contain("_BreadcrumbBar");
    }

    [Fact]
    public void SiteCss_DefinesBreadcrumbBar()
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/site.css"));
        css.Should().Contain(".app-breadcrumb-bar");
    }

    [Fact]
    public void Partial_RendersOnlyNestedTrail()
    {
        var cshtml = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Shared/_BreadcrumbBar.cshtml"));
        cshtml.Should().Contain("@if (Model is { IsNested: true })",
            "zanoření 0 (kořen sekce) lištu nemá — uživatel 2026-10-07");
    }
}
