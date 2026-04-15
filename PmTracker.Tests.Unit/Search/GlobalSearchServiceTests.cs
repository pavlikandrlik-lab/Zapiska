using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Search;

namespace PmTracker.Tests.Unit.Search;

public sealed class GlobalSearchServiceTests
{
    [Fact]
    public async Task KratkyDotaz_VraciPrazdnyVysledek()
    {
        var svc = BuildService(out _);
        var result = await svc.SearchAsync("x", BuildUser(), pageSize: 10, CancellationToken.None);
        result.Hits.Should().BeEmpty();
    }

    [Fact]
    public async Task PostFilter_OdstraniHityMimoScope()
    {
        var svc = BuildService(out var fakeClient);
        fakeClient.Response = new SearchQueryResponse
        {
            TotalCandidates = 3,
            Hits = new[]
            {
                new SearchHit { EntityType = EntityDocumentMapper.TypeZaznam, EntityId = "1", ProjektId = 10, Title = "A" },
                new SearchHit { EntityType = EntityDocumentMapper.TypeZaznam, EntityId = "2", ProjektId = 99, Title = "B" },
                new SearchHit { EntityType = EntityDocumentMapper.TypeOsoba, EntityId = "3", ProjektId = null, Title = "C" },
            }
        };

        var user = BuildUser(visibleProjectIds: new[] { 10 });
        var result = await svc.SearchAsync("hledani", user, pageSize: 10, CancellationToken.None);

        result.Hits.Select(h => h.EntityId).Should().BeEquivalentTo(new[] { "1", "3" });
    }

    [Fact]
    public async Task HasMore_JeTrue_KdyzPageSizePrekrocen()
    {
        var svc = BuildService(out var fakeClient);
        fakeClient.Response = new SearchQueryResponse
        {
            Hits = Enumerable.Range(1, 5)
                .Select(i => new SearchHit { EntityType = EntityDocumentMapper.TypeOsoba, EntityId = i.ToString(), Title = "O" + i })
                .ToArray()
        };
        var result = await svc.SearchAsync("xy", BuildUser(), pageSize: 3, CancellationToken.None);
        result.Hits.Should().HaveCount(3);
        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task Groups_JsouSeskupeny_PodleEntityType()
    {
        var svc = BuildService(out var fakeClient);
        fakeClient.Response = new SearchQueryResponse
        {
            Hits = new[]
            {
                new SearchHit { EntityType = EntityDocumentMapper.TypeOsoba, EntityId = "1", Title = "o1" },
                new SearchHit { EntityType = EntityDocumentMapper.TypeOsoba, EntityId = "2", Title = "o2" },
                new SearchHit { EntityType = EntityDocumentMapper.TypeSubsystem, EntityId = "9", Title = "s1" },
            }
        };
        var result = await svc.SearchAsync("xy", BuildUser(), pageSize: 10, CancellationToken.None);
        result.Groups.Should().ContainKey(EntityDocumentMapper.TypeOsoba);
        result.Groups[EntityDocumentMapper.TypeOsoba].Should().HaveCount(2);
        result.Groups[EntityDocumentMapper.TypeSubsystem].Should().HaveCount(1);
    }

    private static GlobalSearchService BuildService(out FakeSearchClient client)
    {
        client = new FakeSearchClient();
        var options = Options.Create(new SearchOptions { PostFilterCandidateMultiplier = 3 });
        return new GlobalSearchService(client, new FakeEmbeddingService(), options, NullLogger<GlobalSearchService>.Instance);
    }

    private static CurrentUserContextViewModel BuildUser(bool isSuperAdmin = false, IReadOnlyList<int>? visibleProjectIds = null)
        => new()
        {
            OsobaId = 1,
            Jmeno = "T",
            Prijmeni = "U",
            DisplayName = "T U",
            Email = "t@u",
            OrganizacniCelek = "x",
            OrganizacniCelekKod = "X",
            IsSuperAdmin = isSuperAdmin,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = visibleProjectIds ?? Array.Empty<int>(),
            DeletedProjectIds = Array.Empty<int>(),
            PermissionGrants = Array.Empty<PermissionGrantViewModel>()
        };

    private sealed class FakeSearchClient : ISearchClient
    {
        public SearchQueryResponse Response { get; set; } = new();

        public Task EnsureIndexAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task BulkIndexAsync(IReadOnlyCollection<SearchDocument> documents, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteDocumentAsync(string entityType, string entityId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SearchQueryResponse> SearchAsync(SearchQueryRequest request, CancellationToken cancellationToken) => Task.FromResult(Response);
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public Task<IReadOnlyList<float>?> EmbedAsync(string text, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<float>?>(null);
    }
}
