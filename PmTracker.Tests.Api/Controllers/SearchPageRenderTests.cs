using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class SearchPageRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public SearchPageRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    // Stránka výsledků renderuje kartu jen když něco najde, takže si napřed
    // seedneme záznam se „zal" v názvu. AdminOsobaId je superadmin → vidí ho.
    private async Task SeedMatchAsync()
    {
        var projektId = await _fixture.EnsureProjectAsync("SRCHPAGE");
        var subsystemId = await _fixture.EnsureSubsystemAsync("SRCHPAGE", _fixture.AdminOsobaId);
        await _fixture.EnsureRecordAsync(projektId, _fixture.AdminOsobaId, subsystemId, "U", "zaloha render test");
    }

    private async Task<string> GetPageAsync(string query)
    {
        var client = _fixture.Factory.CreateClient();
        var response = await client.GetAsync($"/Search/Index?q={query}&asUser={_fixture.AdminOsobaId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Stranka_PouzivaGovKostruSablony()
    {
        await SeedMatchAsync();
        var html = await GetPageAsync("zal");

        // Kostra podle gov šablony „Výsledky vyhledávání".
        html.Should().Contain("gov-page-heading");
        html.Should().Contain("gov-card");
    }

    [Fact]
    public async Task Stranka_RenderujeKategorieJakoSekce()
    {
        await SeedMatchAsync();
        var html = await GetPageAsync("zal");

        // Kategorie je objekt — markup na ni musí být připravený i při jediné.
        html.Should().Contain("data-search-category");
    }

    [Fact]
    public async Task Stranka_MaZvyrazneniShody()
    {
        await SeedMatchAsync();
        var html = await GetPageAsync("zal");

        html.Should().Contain("app-search-hl", "zvýraznění je stejné jako v dropdownu");
    }
}
