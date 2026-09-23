using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// Závazné pravidlo z docs/specs/offline-deployment.md:
/// aplikace MUSÍ být spustitelná offline. Žádné runtime odkazy na externí CDN.
/// </summary>
public sealed class OfflineAssetsTests
{
    private static DirectoryInfo RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("repozitář s PmTracker.sln musí být dostupný");
        return directory!;
    }

    [Fact]
    public void Layout_NeobsahujeCdnOdkazy()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "Shared", "_Layout.cshtml");
        var content = File.ReadAllText(path);

        content.Should().NotContain("cdn.jsdelivr.net",
            "_Layout.cshtml nesmí načítat assety z jsdelivr CDN (offline-first)");
        content.Should().NotContain("cdnjs.cloudflare.com",
            "_Layout.cshtml nesmí načítat z cloudflare CDN (offline-first)");
        content.Should().NotContain("unpkg.com",
            "_Layout.cshtml nesmí načítat z unpkg CDN (offline-first)");
        content.Should().NotContain("fonts.googleapis.com",
            "_Layout.cshtml nesmí načítat Google Fonts (offline-first)");
        content.Should().NotContain("fonts.gstatic.com",
            "_Layout.cshtml nesmí načítat Google static fonts (offline-first)");
    }

    [Fact]
    public void Layout_Link_A_Script_Pouze_LokalniCesty()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "Views", "Shared", "_Layout.cshtml");
        var content = File.ReadAllText(path);

        // Najdi všechny <link href="..."> a <script src="...">
        var linkMatches = Regex.Matches(content, "<link[^>]*href=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        foreach (Match match in linkMatches)
        {
            var href = match.Groups[1].Value;
            (href.StartsWith("~/", StringComparison.Ordinal) || href.StartsWith("/", StringComparison.Ordinal))
                .Should().BeTrue($"<link href=\"{href}\"> musí být lokální (začínat ~ nebo /), offline-first");
        }

        var scriptMatches = Regex.Matches(content, "<script[^>]*src=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        foreach (Match match in scriptMatches)
        {
            var src = match.Groups[1].Value;
            (src.StartsWith("~/", StringComparison.Ordinal) || src.StartsWith("/", StringComparison.Ordinal))
                .Should().BeTrue($"<script src=\"{src}\"> musí být lokální (začínat ~ nebo /), offline-first");
        }
    }

    [Fact]
    public void SiteCss_NeobsahujeImportUrl_ZExternihoZdroje()
    {
        var path = Path.Combine(RepoRoot().FullName, "PmTracker.Web", "wwwroot", "css", "site.css");
        var content = File.ReadAllText(path);

        // @import url("https://...") je zakázaný (runtime fetch z cizí domény)
        content.Should().NotMatchRegex(
            @"@import\s+url\s*\(\s*[""']?https?://",
            "site.css nesmí obsahovat @import z externího URL (offline-first)");
    }

    [Fact]
    public void SpecDocument_OfflineDeployment_Existuje()
    {
        var specPath = Path.Combine(RepoRoot().FullName, "docs", "specs", "offline-deployment.md");
        File.Exists(specPath).Should().BeTrue($"specifikace offline nasazení musí existovat na {specPath}");
    }
}
