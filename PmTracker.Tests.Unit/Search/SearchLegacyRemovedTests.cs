using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Indexová vrstva se ruší bez náhrady (spec 2026-09-17 §5). Test drží úklid,
/// aby se nevrátila zadními vrátky.
/// </summary>
public sealed class SearchLegacyRemovedTests
{
    [Theory]
    [InlineData("SqlServerSearchClient.cs")]
    [InlineData("OpenSearchClient.cs")]
    [InlineData("ISearchClient.cs")]
    [InlineData("SearchIndexer.cs")]
    [InlineData("SearchReindexHostedService.cs")]
    [InlineData("EntityDocumentMapper.cs")]
    [InlineData("SearchAcl.cs")]
    [InlineData("DbSuggestService.cs")]
    [InlineData("IEmbeddingService.cs")]
    public void ZanikleSoubory_JizNeexistuji(string fileName)
    {
        File.Exists(ResolvePath($"PmTracker.Web/Services/Search/{fileName}"))
            .Should().BeFalse($"{fileName} patřil ke zrušené indexové vrstvě");
    }

    [Fact]
    public void Seed_JizNeobsahujeKlicSearchReindex()
    {
        var seed = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Security/PermissionSeedConfiguration.cs"));

        seed.Should().NotContain("search.reindex");
        seed.Should().Contain("search.index", "vyhledávání zůstává, klíč pro jeho použití taky");
    }

    [Fact]
    public void DbContext_JizNemaCheckpointTabulku()
    {
        var ctx = File.ReadAllText(ResolvePath("PmTracker.Web/Data/PmTrackerDbContext.cs"));

        ctx.Should().NotContain("SearchReindexCheckpoint");
    }

    [Fact]
    public void ProfilIndex_JizNemaAdminKartuVyhledavani()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Profil/Index.cshtml"));

        view.Should().NotContain("data-search-admin-card");
        view.Should().NotContain("data-search-reindex-trigger");
    }
}
