using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>Spec 2026-10-22: pravidla v site.css pro kartu a stránku záznamu (A, B, C).</summary>
public sealed class RecordLayoutCssTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Css => File.ReadAllText(Path.Combine(RepoRoot(), "PmTracker.Web/wwwroot/css/site.css"));

    /// <summary>Pravidlo, jehož selektor začíná na začátku řádku (ne jako součást složeného selektoru).</summary>
    private static string Rule(string selector)
    {
        var match = Regex.Match(Css, "(?m)^" + Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        match.Success.Should().BeTrue($"pravidlo {selector} existuje");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void ExpandIndicator_SitsInCardGutter_OutOfHeaderFlow()
    {
        var indicator = Rule(".record-expand-indicator");
        indicator.Should().Contain("position: absolute", "indikátor nesmí zabírat sloupec ve flexu hlavičky (R1)");
        indicator.Should().Contain("left: 0");
        indicator.Should().Contain("width: 16px", "vejde se do levého odsazení obsahu karty (16 px)");
        indicator.Should().Contain("height: 16px", "čtverec — po otočení o 90° nepřesáhne do textu");
        Rule(".record-content").Should().Contain("position: relative").And.Contain("padding: 14px 16px 16px");
    }
}
