using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Invarianty oprav 2026-07-07 (breadcrumb + projektové menu):
/// 1a) profilový dropdown nad drobečkovou lištou (z-index), 1b) drobečky zarovnané
/// (li margin reset) + podtržení celého odkazu, 2a) menu vlevo (badge bez auto-marginu),
/// 1c) dashboard projektu v cestě. Source-assertion, aby regrese selhala bez běhu appky.
/// </summary>
public sealed class BreadcrumbAndMenuLayoutTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    private static int ZIndexOf(string css, string selector)
    {
        // Najdi blok „selector { ... }" a v něm z-index: N.
        var block = Regex.Match(css, Regex.Escape(selector) + @"\s*\{[^}]*\}", RegexOptions.Singleline);
        block.Success.Should().BeTrue($"selektor {selector} má existovat");
        var z = Regex.Match(block.Value, @"z-index:\s*(\d+)");
        z.Success.Should().BeTrue($"{selector} má mít z-index");
        return int.Parse(z.Groups[1].Value);
    }

    [Fact] // 1a
    public void UserMenuDropdown_StacksAbove_BreadcrumbBar()
    {
        var css = Read("PmTracker.Web/wwwroot/css/site.css");
        ZIndexOf(css, ".user-menu-panel").Should().BeGreaterThan(
            ZIndexOf(css, ".app-breadcrumb-bar"),
            "profilový dropdown se nesmí schovat pod sticky drobečkovou lištu");
    }

    [Fact] // 1b — zarovnání: li margin reset na položce drobečku
    public void BreadcrumbItem_ResetsListItemMargin()
    {
        var css = Read("PmTracker.Web/wwwroot/css/site.css");
        var block = Regex.Match(css, @"\.app-breadcrumb-item\s*\{[^}]*\}", RegexOptions.Singleline);
        block.Success.Should().BeTrue();
        block.Value.Should().MatchRegex(@"margin:\s*0",
            "gov core `ul li { margin-bottom }` jinak roztáhne list a drobečky skáčou");
    }

    [Fact] // 1b — podtržení celého odkazu (i zkratky), ne jen názvu
    public void BreadcrumbLink_UnderlinesWholeLinkOnHover()
    {
        var css = Read("PmTracker.Web/wwwroot/css/site.css");
        css.Should().MatchRegex(@"\.app-breadcrumb-link:hover\s*\{\s*text-decoration:\s*underline");
        css.Should().NotContain(".app-breadcrumb-link:hover .app-breadcrumb-text",
            "hover nesmí podtrhávat jen název — zkratka za oddělovačem musí být podtržená taky");
    }

    [Fact] // 2a — menu vlevo: badge bez auto-marginu
    public void TabsStatusBadge_HasNoAutoLeftMargin()
    {
        var css = Read("PmTracker.Web/wwwroot/css/site.css");
        var block = Regex.Match(css, @"\.tabs-status\s*\{[^}]*\}", RegexOptions.Singleline);
        block.Success.Should().BeTrue();
        block.Value.Should().MatchRegex(@"margin-left:\s*0");

        var detail = Read("PmTracker.Web/Views/Projekty/Detail.cshtml");
        // Badge v řádku tabů nesmí recyklovat page-header třídu s margin-left:auto.
        detail.Should().NotContain("badge project-status-inline tabs-status");
    }

    [Fact] // 1c — dashboard projektu jako podúroveň v cestě
    public void ProjectDashboard_SetsProjectBreadcrumbs_WithDashboardCurrent()
    {
        var controller = Read("PmTracker.Web/Controllers/ProjectDashboardController.cs");
        controller.Should().MatchRegex(@"SetProjectBreadcrumbs\([^;]*currentText:\s*""Dashboard""",
            "dashboard má být Projekty › [projekt] › Dashboard");
    }
}
