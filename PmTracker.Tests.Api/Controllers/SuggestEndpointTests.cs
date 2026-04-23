using System.Net;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class SuggestEndpointTests
{
    private readonly ApiSqlFixture _fixture;

    public SuggestEndpointTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Suggest_BezParametruQ_Vrati200_SePrazdnymPolem()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Search/Suggest?asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ParseJson(response);
        json.GetProperty("hits").GetArrayLength().Should().Be(0,
            "prázdný dotaz musí vrátit prázdné pole hits");
    }

    [Fact]
    public async Task Suggest_KratkyDotaz_1Znak_Vrati200_SePrazdnymPolem()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Search/Suggest?q=a&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ParseJson(response);
        json.GetProperty("hits").GetArrayLength().Should().Be(0,
            "dotaz kratší než 2 znaky musí vrátit prázdné pole");
    }

    [Fact]
    public async Task Suggest_DotazSDelemHits_Vrati200_SJsonPolem()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        // "test" je dostatečně dlouhý dotaz — očekáváme 200 a JSON objekt s polem hits
        var response = await client.GetAsync($"/Search/Suggest?q=test&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Suggest endpoint musí vždy vracet 200 OK");

        var json = await ParseJson(response);
        json.TryGetProperty("hits", out var hits).Should().BeTrue("odpověď musí obsahovat pole hits");
        hits.ValueKind.Should().Be(JsonValueKind.Array, "hits musí být JSON array");
    }

    [Fact]
    public async Task Suggest_VyhledaZaznam_DleNazvu()
    {
        var adminId = _fixture.AdminOsobaId;
        var marker = "SUGGEST_TEST_" + Guid.NewGuid().ToString("N")[..8];

        // Vytvoříme projekt a záznam se specifickým názvem
        var projektId = await _fixture.EnsureProjectAsync("SUGTEST");
        await _fixture.EnsureProjectTeamMemberAsync(projektId, adminId);

        var subsystemId = await _fixture.EnsureSubsystemAsync("SUGSUB", adminId);
        // Seedované kategorie záznamů: U, INFO, ROZHODNUTI (viz db_seed_dev_admin.sql).
        // Původní "REK" neexistoval → EnsureRecordAsync padal na FirstAsync.
        await _fixture.EnsureRecordAsync(projektId, adminId, subsystemId, "U", marker);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Search/Suggest?q={Uri.EscapeDataString(marker)}&asUser={adminId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ParseJson(response);
        var hitsArray = json.GetProperty("hits");

        // Musí najít alespoň jeden hit s title odpovídajícím markeru
        var found = false;
        foreach (var hit in hitsArray.EnumerateArray())
        {
            if (hit.TryGetProperty("title", out var title) && title.GetString()?.Contains(marker) == true)
            {
                found = true;
                break;
            }
        }

        found.Should().BeTrue($"Suggest musí najít záznam s názvem '{marker}'");
    }

    [Fact]
    public async Task Suggest_HitMaSpravnePoloZky()
    {
        var adminId = _fixture.AdminOsobaId;
        var marker = "HITFIELDS_" + Guid.NewGuid().ToString("N")[..8];

        var projektId = await _fixture.EnsureProjectAsync("HITFLD");
        await _fixture.EnsureProjectTeamMemberAsync(projektId, adminId);
        var subsystemId = await _fixture.EnsureSubsystemAsync("HITSUB", adminId);
        // Seedované kategorie záznamů: U, INFO, ROZHODNUTI (viz db_seed_dev_admin.sql).
        // Původní "REK" neexistoval → EnsureRecordAsync padal na FirstAsync.
        await _fixture.EnsureRecordAsync(projektId, adminId, subsystemId, "U", marker);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Search/Suggest?q={Uri.EscapeDataString(marker)}&asUser={adminId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ParseJson(response);
        var hits = json.GetProperty("hits");

        foreach (var hit in hits.EnumerateArray())
        {
            if (hit.TryGetProperty("title", out var title) && title.GetString()?.Contains(marker) == true)
            {
                hit.TryGetProperty("type", out _).Should().BeTrue("hit musí mít pole type");
                hit.TryGetProperty("url", out _).Should().BeTrue("hit musí mít pole url");
                hit.TryGetProperty("projektNazev", out _).Should().BeTrue("hit musí mít pole projektNazev");
                return;
            }
        }

        // Pokud záznam nebyl nalezen, test failuje s popisnou chybou
        false.Should().BeTrue($"Suggest musí najít záznam '{marker}' a hit musí mít požadovaná pole");
    }

    private static async Task<JsonElement> ParseJson(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }
}
