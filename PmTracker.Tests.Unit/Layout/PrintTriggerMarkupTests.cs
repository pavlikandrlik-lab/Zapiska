using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// A6 (2026-07-08): pm-button print trigger NESMÍ mít href — gov-button s href aktivuje
/// interní anchor vlastní logikou a otevře tab dřív, než uživatel zvolí formát v chooseru.
/// Nativní &lt;a&gt; triggery (karta jednání, řádek záznamu) href mít SMÍ (preventDefault funguje).
/// </summary>
public sealed class PrintTriggerMarkupTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }
    private static string Read(string rel) => File.ReadAllText(Path.Combine(RepoRoot(), rel));

    /// <summary>Vrátí všechny pm-button otevírací tagy obsahující data-print-trigger.</summary>
    private static IEnumerable<string> PmButtonPrintTriggers(string source)
        => Regex.Matches(source, @"<pm-button[^>]*data-print-trigger[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value);

    [Theory]
    [InlineData("PmTracker.Web/Views/Projekty/Detail.cshtml")]
    [InlineData("PmTracker.Web/Views/Jednani/Detail.cshtml")]
    public void PmButtonPrintTriggers_HaveNoHref_ButKeepDataUrls(string rel)
    {
        var src = Read(rel);
        var triggers = PmButtonPrintTriggers(src).ToList();
        triggers.Should().NotBeEmpty("stránka má mít pm-button print trigger");
        foreach (var t in triggers)
        {
            t.Should().NotContain("href=", "gov-button s href otevře tab dřív než chooser (A6)");
            t.Should().NotContain("target=");
            t.Should().Contain("data-print-pdf-url");
            t.Should().Contain("data-print-word-url");
        }
    }

    [Theory]
    [InlineData("PmTracker.Web/Views/Shared/_MeetingCard.cshtml")]
    [InlineData("PmTracker.Web/Views/Projekty/_ZaznamPartial.cshtml")]
    public void NativeAnchorPrintTriggers_KeepHrefFallback(string rel)
    {
        var src = Read(rel);
        var anchors = Regex.Matches(src, @"<a[^>]*data-print-trigger[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value).ToList();
        anchors.Should().NotBeEmpty();
        anchors.Should().OnlyContain(a => a.Contains("href="), "nativní <a> drží href fallback (middle-click)");
    }
}
