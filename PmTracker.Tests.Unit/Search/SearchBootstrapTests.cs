using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Inbox #16 (2026-04-21): Search FTS bootstrap při startu.
///
/// Starý problém: EnsureIndexAsync se volal JEN z IndexEntityAsync/FullReindexAsync.
/// Na nové DB (bez AuthzAuditLog entries pro existující data) se nikdy nezavolal → FTS
/// index chyběl → FREETEXTTABLE padal → search vracel tichý prázdný výsledek.
///
/// Fix: SearchReindexHostedService.RunBootstrapAsync volá EnsureIndex při startu,
/// detekuje prázdný index a spustí full reindex; ISearchClient rozšířen o
/// GetDocumentCountAsync + IsSearchableAsync pro status UI.
/// </summary>
public sealed class SearchBootstrapTests
{
    [Fact]
    public void ReindexHostedService_ShouldCallBootstrapBeforeMainLoop()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Search/SearchReindexHostedService.cs"));

        code.Should().Contain("RunBootstrapAsync",
            "hosted service musí při startu provolat EnsureIndex + případný úvodní full reindex");
        code.Should().MatchRegex(
            @"protected override async Task ExecuteAsync[\s\S]*?RunBootstrapAsync[\s\S]*?while \(!stoppingToken\.IsCancellationRequested\)",
            "bootstrap se musí volat PŘED main loopem, jinak reindex nikdy neproběhne dokud " +
            "nejsou nové audit entries");
    }

    [Fact]
    public void ReindexHostedService_Bootstrap_ShouldHandleMissingFtsPermission()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Search/SearchReindexHostedService.cs"));

        code.Should().Contain("IsSearchableAsync",
            "bootstrap musí ověřit, že FTS index skutečně existuje — jinak loguje chybu " +
            "s návodem pro DBA (CREATE FULLTEXT CATALOG + CREATE FULLTEXT INDEX)");
        code.Should().Contain("CREATE FULLTEXT CATALOG",
            "logovaná chyba musí obsahovat konkrétní SQL příkazy pro DBA");
    }

    [Fact]
    public void SearchClient_ShouldExposeDocumentCountAndSearchableStatus()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Search/ISearchClient.cs"));

        code.Should().Contain("GetDocumentCountAsync");
        code.Should().Contain("IsSearchableAsync");
    }

    [Fact]
    public void SqlServerSearchClient_ShouldImplementBothStatusMethods()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Search/SqlServerSearchClient.cs"));

        code.Should().MatchRegex(
            @"public\s+async\s+Task<long>\s+GetDocumentCountAsync",
            "SqlServerSearchClient musí implementovat GetDocumentCountAsync");
        code.Should().MatchRegex(
            @"public\s+async\s+Task<bool>\s+IsSearchableAsync",
            "SqlServerSearchClient musí implementovat IsSearchableAsync");
        code.Should().Contain("sys.fulltext_indexes",
            "IsSearchableAsync kontroluje přítomnost FTS indexu přes sys.fulltext_indexes");
    }

    [Fact]
    public void SearchController_ShouldHaveStatusEndpoint()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Controllers/SearchController.cs"));

        code.Should().MatchRegex(
            @"public\s+async\s+Task<IActionResult>\s+Status\s*\(",
            "SearchController musí mít GET /Search/Status endpoint pro status panel v Profil/Index");
        code.Should().Contain("HasPermission",
            "Status endpoint je chráněný — přístup řídí HasPermission(SearchReindex)"); // C6: IsSuperAdmin → HasPermission
    }

    [Fact]
    public void ProfilIndex_ShouldShowSearchAdminCardForSuperAdmin()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Profil/Index.cshtml"));

        view.Should().Contain("data-search-admin-card",
            "Profil/Index obsahuje kartu pro super-admina s search status + reindex tlačítkem");
        view.Should().Contain("Model.Uzivatel.IsSuperAdmin",
            "karta je podmíněně renderována JEN pro super-admina");
        view.Should().Contain("data-search-reindex-trigger",
            "karta má tlačítko pro spuštění reindexu");
    }
}
