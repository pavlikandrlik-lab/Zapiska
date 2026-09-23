using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// DS gov 4.7.0 se servíruje z wwwroot/assets/gov se správným typem a natrvalo
/// cachovaný (spec 2026-09-23 §4.4). Factory běží v prostředí Development.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class GovStaticAssetsServingTests
{
    private readonly ApiSqlFixture _fixture;

    public GovStaticAssetsServingTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Font_MaTypWoff2ACacheImmutable()
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/assets/gov/fonts/roboto-regular.woff2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("font/woff2");
        response.Headers.CacheControl!.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task LoaderKomponent_MaCharsetUtf8ACacheImmutable()
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/assets/gov/components/core.esm.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.ToString().Should().Contain("charset=utf-8");
        response.Headers.CacheControl!.ToString().Should().Contain("immutable");
    }

    [Fact]
    public async Task AplikacniJs_VeVyvoji_ZustavaNoCache()
    {
        using var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync("/js/global-search.js");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
}
