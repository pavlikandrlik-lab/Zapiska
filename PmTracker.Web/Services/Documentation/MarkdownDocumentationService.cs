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

    private static readonly IReadOnlyList<DocumentationSource> Sources =
    [
        new(
            "tech-documentation-tree",
            "Strom dokumentace",
            "Rekurzivní strom oblastí, podoblastí a listových uzlů.",
            "/Dokumentace/Technicka/Strom-dokumentace",
            ["technical/00-documentation-tree.md"],
            DocumentationArea.Technical),
        new(
            "tech-system-context",
            "Systémový kontext",
            "Hranice systému, integrace a provozní ownership.",
            "/Dokumentace/Technicka/Systemovy-kontext",
            ["technical/01-system-context.md"],
            DocumentationArea.Technical),
        new(
            "tech-architecture",
            "Architektura",
            "Komponenty, vrstvy a pravidla návrhu změn.",
            "/Dokumentace/Technicka/Architektura",
            ["technical/02-architecture.md"],
            DocumentationArea.Technical),
        new(
            "tech-runtime-configuration",
            "Runtime konfigurace",
            "Povinné klíče konfigurace, environment a fail-fast pravidla.",
            "/Dokumentace/Technicka/Runtime-konfigurace",
            ["technical/03-runtime-configuration.md"],
            DocumentationArea.Technical),
        new(
            "tech-installation-deployment-iis",
            "Instalace a deployment (IIS)",
            "End-to-end instalační a release postup pro IIS in-process.",
            "/Dokumentace/Technicka/Instalace-a-deployment-iis",
            ["technical/04-installation-deployment-iis.md"],
            DocumentationArea.Technical),
        new(
            "tech-web-server-iis-config",
            "Konfigurace web serveru IIS",
            "Standard app pool, site, auth a web.config nastavení.",
            "/Dokumentace/Technicka/Web-server-iis",
            ["technical/05-web-server-iis-config.md"],
            DocumentationArea.Technical),
        new(
            "tech-database-bootstrap-migrations",
            "Databáze, bootstrap a migrace",
            "Pořadí SQL kroků, baseline, patching a DBA rollback.",
            "/Dokumentace/Technicka/Databaze-bootstrap-a-migrace",
            ["technical/06-database-bootstrap-migrations.md"],
            DocumentationArea.Technical),
        new(
            "tech-security-authz",
            "Bezpečnost a autorizace",
            "Windows auth, permission model a správa efektivních práv.",
            "/Dokumentace/Technicka/Bezpecnost-a-opravneni",
            ["technical/07-security-authz.md"],
            DocumentationArea.Technical),
        new(
            "tech-operations-runbooks",
            "Provozní runbooky",
            "SOP postupy pro restart, cutover, onboarding a incident response.",
            "/Dokumentace/Technicka/Provozni-runbooky",
            ["technical/08-operations-runbooks.md"],
            DocumentationArea.Technical),
        new(
            "tech-testing-quality",
            "Testování a kvalita",
            "Test pipeline, coverage, mutation a dokumentační quality gate.",
            "/Dokumentace/Technicka/Testovani-a-kvalita",
            ["technical/09-testing-quality.md"],
            DocumentationArea.Technical),
        new(
            "tech-troubleshooting-recovery",
            "Troubleshooting a recovery",
            "Incident triáž, rozhodovací strom recovery a post-incident audit.",
            "/Dokumentace/Technicka/Troubleshooting-a-recovery",
            ["technical/10-troubleshooting-recovery.md"],
            DocumentationArea.Technical),
        new(
            "user-guide",
            "Uživatelská příručka",
            "Praktický návod pro běžné uživatele aplikace.",
            "/Dokumentace/Uzivatelska-prirucka",
            ["user-guide.md"],
            DocumentationArea.Supporting),
        new(
            "qa",
            "Q and A",
            "Často kladené dotazy k provozu aplikace.",
            "/Dokumentace/qa",
            ["qa.md"],
            DocumentationArea.Supporting),
        new(
            "changelog",
            "Changelog verzí",
            "Přehled změn mezi verzemi aplikace.",
            "/Dokumentace/Changelog",
            ["changelog.md"],
            DocumentationArea.Supporting)
    ];

    private static readonly IReadOnlyDictionary<string, DocumentationSource> SourceLookup =
        Sources.ToDictionary(source => source.Key, StringComparer.OrdinalIgnoreCase);

    private readonly IWebHostEnvironment _environment;
    private readonly MarkdownPipeline _pipeline;

    public MarkdownDocumentationService(IWebHostEnvironment environment)
    {
        _environment = environment;
        _pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()
            .UseAutoLinks()
            .UseGenericAttributes()
            .UseEmphasisExtras()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .DisableHtml()
            .Build();
    }

    public DocumentationPageViewModel BuildPage(string key)
    {
        var normalizedKey = NormalizeKey(key);
        if (!SourceLookup.TryGetValue(normalizedKey, out var source))
        {
            throw new InvalidOperationException($"Neznámá dokumentační stránka '{key}'.");
        }

        var markdown = LoadMarkdown(source.Files);
        var html = Markdown.ToHtml(markdown, _pipeline);

        return new DocumentationPageViewModel
        {
            Key = source.Key,
            SectionLabel = source.Area == DocumentationArea.Technical
                ? "Technická dokumentace"
                : "Uživatelská a provozní dokumentace",
            Title = source.Title,
            Subtitle = source.Subtitle,
            CanonicalPath = source.Path,
            HtmlContent = html,
            Navigation = BuildNavigation(source.Key),
            TechnicalNavigation = BuildNavigation(source.Key, DocumentationArea.Technical),
            SupportingNavigation = BuildNavigation(source.Key, DocumentationArea.Supporting),
            TocItems = BuildToc(html)
        };
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "tech-documentation-tree" : key.Trim().ToLowerInvariant();
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

    private static IReadOnlyList<DocumentationNavItemViewModel> BuildNavigation(string activeKey, DocumentationArea? area = null)
    {
        return Sources
            .Where(source => !area.HasValue || source.Area == area.Value)
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

    private enum DocumentationArea
    {
        Technical,
        Supporting
    }

    private sealed record DocumentationSource(
        string Key,
        string Title,
        string Subtitle,
        string Path,
        IReadOnlyList<string> Files,
        DocumentationArea Area);
}
