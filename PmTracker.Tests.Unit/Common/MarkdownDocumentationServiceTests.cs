using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using PmTracker.Web.Services.Documentation;

namespace PmTracker.Tests.Unit.Common;

public sealed class MarkdownDocumentationServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public MarkdownDocumentationServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "pmtracker-docs-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "DocsContent", "technical"));
        SeedDocsContent();
    }

    [Fact]
    public void BuildPage_ShouldRenderTechnicalNavigationAndToc()
    {
        var sut = new MarkdownDocumentationService(new TestWebHostEnvironment(_tempRoot));

        var result = sut.BuildPage("tech-documentation-tree");

        result.Key.Should().Be("tech-documentation-tree");
        result.CanonicalPath.Should().Be("/Dokumentace/Technicka/Strom-dokumentace");
        result.TechnicalNavigation.Should().HaveCount(11);
        result.SupportingNavigation.Should().HaveCount(3);
        result.TechnicalNavigation.Should().ContainSingle(item => item.Key == "tech-documentation-tree" && item.IsActive);
        result.SupportingNavigation.Should().OnlyContain(item => !item.IsActive);
        result.TocItems.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildPage_ShouldRenderSupportingNavigationForChangelog()
    {
        var sut = new MarkdownDocumentationService(new TestWebHostEnvironment(_tempRoot));

        var result = sut.BuildPage("changelog");

        result.Key.Should().Be("changelog");
        result.Title.Should().Be("Changelog verzí");
        result.CanonicalPath.Should().Be("/Dokumentace/Changelog");
        result.SupportingNavigation.Should().ContainSingle(item => item.Key == "changelog" && item.IsActive);
        result.TechnicalNavigation.Should().OnlyContain(item => !item.IsActive);
    }

    [Fact]
    public void BuildPage_ShouldResolveAllMappedKeys()
    {
        var sut = new MarkdownDocumentationService(new TestWebHostEnvironment(_tempRoot));
        var keys = new[]
        {
            "tech-documentation-tree",
            "tech-system-context",
            "tech-architecture",
            "tech-runtime-configuration",
            "tech-installation-deployment-iis",
            "tech-web-server-iis-config",
            "tech-database-bootstrap-migrations",
            "tech-security-authz",
            "tech-operations-runbooks",
            "tech-testing-quality",
            "tech-troubleshooting-recovery",
            "user-guide",
            "qa",
            "changelog"
        };

        foreach (var key in keys)
        {
            var page = sut.BuildPage(key);
            page.Key.Should().Be(key);
            page.HtmlContent.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void BuildPage_ShouldDefaultToDocumentationTree_WhenKeyIsEmpty()
    {
        var sut = new MarkdownDocumentationService(new TestWebHostEnvironment(_tempRoot));

        var page = sut.BuildPage(string.Empty);

        page.Key.Should().Be("tech-documentation-tree");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private void SeedDocsContent()
    {
        var docs = new Dictionary<string, string>
        {
            ["technical/00-documentation-tree.md"] = "# 00\n\n## Sekce A\nText",
            ["technical/01-system-context.md"] = "# 01\n\n## Sekce A\nText",
            ["technical/02-architecture.md"] = "# 02\n\n## Sekce A\nText",
            ["technical/03-runtime-configuration.md"] = "# 03\n\n## Sekce A\nText",
            ["technical/04-installation-deployment-iis.md"] = "# 04\n\n## Sekce A\nText",
            ["technical/05-web-server-iis-config.md"] = "# 05\n\n## Sekce A\nText",
            ["technical/06-database-bootstrap-migrations.md"] = "# 06\n\n## Sekce A\nText",
            ["technical/07-security-authz.md"] = "# 07\n\n## Sekce A\nText",
            ["technical/08-operations-runbooks.md"] = "# 08\n\n## Sekce A\nText",
            ["technical/09-testing-quality.md"] = "# 09\n\n## Sekce A\nText",
            ["technical/10-troubleshooting-recovery.md"] = "# 10\n\n## Sekce A\nText",
            ["user-guide.md"] = "# User\n\n## Sekce\nText",
            ["qa.md"] = "# QA\n\n## Sekce\nText",
            ["changelog.md"] = "## 1.0 - 2026-03-12\n\n### Změněno\n- Test"
        };

        foreach (var (relativePath, content) in docs)
        {
            var fullPath = Path.Combine(_tempRoot, "DocsContent", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PmTracker.Tests.Unit";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
