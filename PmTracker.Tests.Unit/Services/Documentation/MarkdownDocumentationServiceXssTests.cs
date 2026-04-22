using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using PmTracker.Web.Services.Documentation;

namespace PmTracker.Tests.Unit.Services.Documentation;

/// <summary>
/// Regression guard for QW-5: DisableHtml() must prevent raw HTML execution via Markdig.
///
/// Markdig's DisableHtml() escapes &lt; and &gt; in raw HTML blocks so the
/// injected tags become inert text rendered as &amp;lt;script&amp;gt;... in the browser.
/// The tests assert that the active HTML tag forms (&lt;script&gt;, &lt;iframe&gt;, etc.)
/// do NOT appear in the rendered output, only their HTML-encoded counterparts.
/// If DisableHtml() is ever removed from the pipeline builder these tests fail immediately.
/// </summary>
public sealed class MarkdownDocumentationServiceXssTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly MarkdownDocumentationService _sut;

    public MarkdownDocumentationServiceXssTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "pmtracker-xss-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(_tempRoot, "DocsContent", "technical"));
        SeedMinimalDocsContent();

        _sut = new MarkdownDocumentationService(new TestWebHostEnvironment(_tempRoot));
    }

    // ---------------------------------------------------------------
    // XSS injection tests
    // DisableHtml() HTML-encodes the tags, so we assert the RAW tag
    // form is absent — not that the attribute text is missing.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<script src=\"evil.js\"></script>")]
    public void Render_ShouldNotEmitExecutable_ScriptTags(string malicious)
    {
        // Arrange — inject into a documentation page
        OverwriteDocContent("technical/00-documentation-tree.md",
            $"# Heading\n\n{malicious}\n\nNormal paragraph.");

        // Act
        var page = _sut.BuildPage("tech-documentation-tree");

        // Assert — DisableHtml() escapes < to &lt; so the tag can never execute
        page.HtmlContent.Should().NotContain("<script",
            "raw <script> open-tag must be HTML-encoded by DisableHtml() — QW-5 regression guard; " +
            "if present the browser would execute the script");
    }

    [Fact]
    public void Render_ShouldNotEmitExecutable_IframeTag()
    {
        // Arrange
        OverwriteDocContent("technical/00-documentation-tree.md",
            "# Heading\n\n<iframe src=\"https://evil.example.com\"></iframe>\n\nNormal text.");

        // Act
        var page = _sut.BuildPage("tech-documentation-tree");

        // Assert
        page.HtmlContent.Should().NotContain("<iframe",
            "raw <iframe> open-tag must be HTML-encoded by DisableHtml() — QW-5 regression guard");
    }

    [Fact]
    public void Render_ShouldNotEmitExecutable_ImgOnerrorTag()
    {
        // Arrange
        OverwriteDocContent("technical/00-documentation-tree.md",
            "# Heading\n\n<img src=x onerror=alert(1)>\n\nNormal text.");

        // Act
        var page = _sut.BuildPage("tech-documentation-tree");

        // Assert — DisableHtml() escapes < so the <img …> tag itself is never emitted as HTML
        page.HtmlContent.Should().NotContain("<img ",
            "raw <img> tag must be HTML-encoded; if the <img> element is emitted the onerror handler fires");
    }

    [Fact]
    public void Render_ShouldNotEmitExecutable_JavascriptUriAnchor()
    {
        // Arrange
        OverwriteDocContent("technical/00-documentation-tree.md",
            "# Heading\n\n<a href=\"javascript:alert(1)\">click me</a>\n\nNormal text.");

        // Act
        var page = _sut.BuildPage("tech-documentation-tree");

        // Assert — DisableHtml() must escape the raw <a href="javascript:…"> tag
        page.HtmlContent.Should().NotContain("<a href=\"javascript:",
            "raw <a href=\"javascript:…\"> must be HTML-encoded — QW-5 regression guard");
    }

    [Fact]
    public void Render_ShouldPreserveNormalMarkdown_WhenNoMaliciousContent()
    {
        // Arrange — normal markdown must still render correctly after DisableHtml()
        OverwriteDocContent("technical/00-documentation-tree.md",
            "# Heading\n\n## Sub-heading\n\nNormal **bold** and _italic_ text.\n\n- list item");

        // Act
        var page = _sut.BuildPage("tech-documentation-tree");

        // Assert — sanitisation must not break normal markdown content
        page.HtmlContent.Should().Contain("<strong>bold</strong>",
            "bold markdown must still render as <strong>");
        page.HtmlContent.Should().Contain("<em>italic</em>",
            "italic markdown must still render as <em>");
        page.HtmlContent.Should().Contain("<li>list item</li>",
            "list items must still render");
    }

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void OverwriteDocContent(string relativePath, string content)
    {
        var fullPath = Path.Combine(
            _tempRoot,
            "DocsContent",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private void SeedMinimalDocsContent()
    {
        // Seed all 14 keys that MarkdownDocumentationService expects so that
        // BuildPage() doesn't throw FileNotFoundException for unrelated pages.
        var docs = new Dictionary<string, string>
        {
            ["technical/00-documentation-tree.md"] = "# 00\n\nPlaceholder.",
            ["technical/01-system-context.md"] = "# 01",
            ["technical/02-architecture.md"] = "# 02",
            ["technical/03-runtime-configuration.md"] = "# 03",
            ["technical/04-installation-deployment-iis.md"] = "# 04",
            ["technical/05-web-server-iis-config.md"] = "# 05",
            ["technical/06-database-bootstrap-migrations.md"] = "# 06",
            ["technical/07-security-authz.md"] = "# 07",
            ["technical/08-operations-runbooks.md"] = "# 08",
            ["technical/09-testing-quality.md"] = "# 09",
            ["technical/10-troubleshooting-recovery.md"] = "# 10",
            ["user-guide.md"] = "# User",
            ["qa.md"] = "# QA",
            ["changelog.md"] = "## 1.0"
        };

        foreach (var (path, content) in docs)
        {
            var fullPath = Path.Combine(
                _tempRoot,
                "DocsContent",
                path.Replace('/', Path.DirectorySeparatorChar));
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
