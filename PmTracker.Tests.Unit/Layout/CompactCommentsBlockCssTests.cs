using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>Spec 2026-10-22 (vyjádření v jednom bloku) — pravidla v site.css.</summary>
public sealed class CompactCommentsBlockCssTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    private static string Rule(string selector)
    {
        var match = Regex.Match(Css, Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        match.Success.Should().BeTrue($"pravidlo {selector} existuje");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void Block_HasBorder_AndDoesNotClipTooltip()
    {
        var body = Rule(".record-comments .comment-list");
        body.Should().Contain("border: 1px solid var(--border-subtle)");
        body.Should().NotContain("overflow", "bublina u posledního řádku nesmí být oříznutá");
    }

    [Fact]
    public void Row_HasNoOwnBorder_AndGreyHover()
    {
        var row = Rule(".record-comments .comment");
        row.Should().Contain("border: 0");
        row.Should().Contain("display: flow-root");
        row.Should().Contain("overflow: visible",
            "základní .comment (sdílený s _TaskItemPartial) má overflow: hidden — v bloku by ořízl bublinu i ikony");
        Regex.IsMatch(Css, @"\.record-comments \.comment:hover,\s*\.record-comments \.comment:focus-within\s*\{[^}]*background: var\(--background-neutral-subtle\)")
            .Should().BeTrue("jemně šedé podbarvení řádku pod myší i při fokusu (R2, R9)");
    }

    [Fact]
    public void RowActions_HiddenUntilRowHoverOrFocus()
    {
        var actions = Rule(".record-comments .comment-row-actions");
        actions.Should().Contain("opacity: 0");
        actions.Should().Contain("pointer-events: none");
        actions.Should().Contain("position: absolute", "ikony nesmí posunout text ani zvýšit řádek");
        Regex.IsMatch(Css, @"\.record-comments \.comment:hover \.comment-row-actions,\s*\.record-comments \.comment:focus-within \.comment-row-actions\s*\{[^}]*opacity: 1")
            .Should().BeTrue();
    }

    [Fact]
    public void Tooltip_EndVariant_AlignsToRightEdge()
    {
        Rule(".chip-tooltip--end").Should().Contain("right: 0");
    }

    [Fact]
    public void RemovedPermanentMetaLine_HasNoCss()
    {
        foreach (var dead in new[] { ".comment-wrapper", ".comment-meta-outer", ".comment-author", ".comment-sep", ".comment-date", ".comment-tile-side" })
        {
            Css.Should().NotContain(dead + " {", $"{dead} už markup nepoužívá");
        }
    }
}
