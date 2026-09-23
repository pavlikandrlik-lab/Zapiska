using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A3 (2026-07-08): --d-* tokeny musí být v :root (subpages bez .dashboard-shell jinak
/// resolvnou var() na nic → slepené karty). Bottom gap sjednocen tokenem
/// --app-content-bottom-gap aplikovaným na dashboard i projektová jednání.
/// </summary>
public sealed class DashboardTokenScopeTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    private static string BlockOf(string css, string selectorRegex)
    {
        var m = Regex.Match(css, selectorRegex + @"\s*\{[^}]*\}", RegexOptions.Singleline);
        m.Success.Should().BeTrue($"blok {selectorRegex} má existovat");
        return m.Value;
    }

    [Theory]
    [InlineData("--d-fs-label")]
    [InlineData("--d-fs-base")]
    [InlineData("--d-fs-title")]
    [InlineData("--d-gap-item")]
    [InlineData("--d-gap-panel")]
    [InlineData("--d-pad-card")]
    [InlineData("--d-pad-panel")]
    [InlineData("--d-radius")]
    [InlineData("--d-panel-min")]
    public void DashboardTokens_AreDefinedInRoot_NotInShell(string token)
    {
        var css = Css;
        Regex.IsMatch(css, @":root\s*\{[^}]*" + Regex.Escape(token) + @"\s*:", RegexOptions.Singleline)
            .Should().BeTrue($"{token} musí být definován v :root");
        var shell = BlockOf(css, @"\.dashboard-shell");
        shell.Should().NotContain(token + ":", $"{token} nesmí být re-definován v .dashboard-shell (drift)");
    }

    [Fact]
    public void ContentBottomGap_TokenDefined_AndApplied()
    {
        var css = Css;
        css.Should().Contain("--app-content-bottom-gap: 20px");
        css.Should().MatchRegex(@"\.app-main--fluid:has\(\.dashboard-shell\)\s*\{[^}]*padding-bottom:\s*var\(--app-content-bottom-gap\)");
        css.Should().MatchRegex(@"\.dashboard-list-page[^{]*\{[^}]*padding-bottom:\s*var\(--app-content-bottom-gap\)");
        css.Should().MatchRegex(@"\.meeting-year-stack\s*\{[^}]*margin-bottom:\s*var\(--app-content-bottom-gap\)");
    }
}
