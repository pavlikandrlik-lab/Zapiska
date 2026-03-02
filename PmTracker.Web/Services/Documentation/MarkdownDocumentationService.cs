using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Documentation;

public sealed class MarkdownDocumentationService : IDocumentationService
{
    private static readonly Regex TocHeadingRegex = new(
        "<h([23])\\s+id=\"([^\"]+)\">(.*?)</h\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    // One central map keeps routing + source files aligned.
    // Installation markdown remains in repo but is intentionally not exposed in-app.
    private static readonly IReadOnlyDictionary<string, DocumentationSource> Sources = new Dictionary<string, DocumentationSource>(StringComparer.OrdinalIgnoreCase)
    {
        ["technicka"] = new("technicka", "Technická dokumentace", "Architektura, provozní pravidla, testování a údržba aplikace.", "/Dokumentace/Technicka-dokumentace", new[] { "technical-guide.md" }),
        ["admin"] = new("admin", "Administrační dokumentace", "Správa rolí, oprávnění, číselníků a provozu.", "/Dokumentace/Administracni-prirucka", new[] { "admin-guide.md" }),
        ["uzivatelska"] = new("uzivatelska", "Uživatelská příručka", "Praktický návod pro běžné uživatele aplikace.", "/Dokumentace/Uzivatelska-prirucka", new[] { "user-guide.md" }),
        ["qa"] = new("qa", "Q and A", "Často kladené dotazy k provozu aplikace.", "/Dokumentace/qa", new[] { "qa.md" })
    };

    private readonly IWebHostEnvironment _environment;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownDocumentationService(IWebHostEnvironment environment)
    {
        _environment = environment;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .Build();
    }

    public DocumentationPageViewModel BuildPage(string key)
    {
        var normalizedKey = NormalizeKey(key);
        if (!Sources.TryGetValue(normalizedKey, out var source))
        {
            throw new InvalidOperationException($"Neznámá dokumentační stránka '{key}'.");
        }

        var markdown = LoadMarkdown(source.Files);
        var html = Markdown.ToHtml(markdown, _pipeline);

        return new DocumentationPageViewModel
        {
            Key = source.Key,
            Title = source.Title,
            Subtitle = source.Subtitle,
            CanonicalPath = source.Path,
            HtmlContent = html,
            Navigation = BuildNavigation(source.Key),
            TocItems = BuildToc(html)
        };
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "technicka" : key.Trim().ToLowerInvariant();
    }

    private string LoadMarkdown(IReadOnlyList<string> files)
    {
        var docsRoot = ResolveDocsRoot();
        var parts = new List<string>(files.Count);

        foreach (var file in files)
        {
            var fullPath = Path.Combine(docsRoot, file);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Dokumentace nebyla nalezena: {fullPath}");
            }

            parts.Add(File.ReadAllText(fullPath));
        }

        return string.Join(Environment.NewLine + Environment.NewLine + "---" + Environment.NewLine + Environment.NewLine, parts);
    }

    private string ResolveDocsRoot()
    {
        // Prefer file layout from the running app (publish output),
        // fallback to the same location during local development.
        var contentRootDocs = Path.Combine(_environment.ContentRootPath, "DocsContent");
        if (Directory.Exists(contentRootDocs))
        {
            return contentRootDocs;
        }

        var runtimeDocs = Path.Combine(AppContext.BaseDirectory, "DocsContent");
        if (Directory.Exists(runtimeDocs))
        {
            return runtimeDocs;
        }

        throw new DirectoryNotFoundException(
            $"Nebyla nalezena složka s dokumentací 'DocsContent'. Zkoušeno: '{contentRootDocs}', '{runtimeDocs}'.");
    }

    private static IReadOnlyList<DocumentationNavItemViewModel> BuildNavigation(string activeKey)
    {
        return Sources.Values
            .Select(source => new DocumentationNavItemViewModel
            {
                Key = source.Key,
                Label = source.Title,
                Path = source.Path,
                IsActive = source.Key.Equals(activeKey, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
    }

    private static IReadOnlyList<DocumentationTocItemViewModel> BuildToc(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<DocumentationTocItemViewModel>();
        }

        var items = new List<DocumentationTocItemViewModel>();
        foreach (Match match in TocHeadingRegex.Matches(html))
        {
            var levelRaw = match.Groups[1].Value;
            var id = match.Groups[2].Value;
            var innerHtml = match.Groups[3].Value;

            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var label = StripHtml(innerHtml);
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            _ = int.TryParse(levelRaw, out var level);
            items.Add(new DocumentationTocItemViewModel
            {
                Id = id,
                Label = label,
                Level = level <= 0 ? 2 : level
            });
        }

        return items;
    }

    private static string StripHtml(string value)
    {
        var withoutTags = Regex.Replace(value, "<.*?>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    private sealed record DocumentationSource(
        string Key,
        string Title,
        string Subtitle,
        string Path,
        IReadOnlyList<string> Files);
}
