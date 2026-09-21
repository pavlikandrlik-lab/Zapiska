using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class SearchEndpointsTests
{
    private readonly ApiSqlFixture _fixture;

    public SearchEndpointsTests(ApiSqlFixture fixture) => _fixture = fixture;

    // TestAuthHandler přihlásí osobu podle ?asUser; AdminOsobaId je seedovaný superadmin.
    private string AsUser => $"asUser={_fixture.AdminOsobaId}";

    [Fact]
    public async Task Suggest_PodPrahemTriZnaku_VraciPrazdnoBezChyby()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Search/Suggest?q=za&{AsUser}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Suggest_VraciOcekavanyTvarJson()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Search/Suggest?q=zal&{AsUser}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.TryGetProperty("query", out _).Should().BeTrue();
        json.TryGetProperty("totalCount", out _).Should().BeTrue();
        json.TryGetProperty("categories", out var categories).Should().BeTrue();
        categories.ValueKind.Should().Be(JsonValueKind.Array,
            "kategorie jsou pole, aby šlo přidat další bez změny kontraktu");
    }

    [Fact]
    public async Task Index_SeVykresli()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Search/Index?q=zal&{AsUser}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reindex_UzNeexistuje()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsync($"/Search/Reindex?{AsUser}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "endpoint zanikl spolu s indexovou vrstvou");
    }

    [Fact]
    public async Task Status_UzNeexistuje()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/Search/Status?{AsUser}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
