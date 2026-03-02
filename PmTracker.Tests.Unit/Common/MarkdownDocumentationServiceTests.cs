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
        Directory.CreateDirectory(Path.Combine(_tempRoot, "DocsContent"));
    }

    [Fact]
    public void BuildPage_ShouldLoadChangelogFromDocsContent_AndExposeNavigationItem()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "DocsContent", "changelog.md"), """
## 0.3 - 2026-03-02

### Změněno
- Přidána dokumentační záložka changelogu.
""");

        var sut = new MarkdownDocumentationService(new TestWebHostEnvironment(_tempRoot));

        var result = sut.BuildPage("changelog");

        result.Key.Should().Be("changelog");
        result.Title.Should().Be("Changelog verzí");
        result.CanonicalPath.Should().Be("/Dokumentace/Changelog");
        result.Navigation.Should().ContainSingle(item => item.Key == "changelog" && item.IsActive);
        result.HtmlContent.Should().Contain("Přidána dokumentační záložka changelogu.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
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
