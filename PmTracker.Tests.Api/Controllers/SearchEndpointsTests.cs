using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

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

    [Fact]
    public async Task Suggest_ZastupceVedoucihoSubsystemu_NajdeZaznamSvehoProjektu()
    {
        // Regrese 89bb967: permission:search.index bez projektId = globální kontrola → 403 všem
        // bez globální role. Hledání hlídá jen přihlášení, rozsah dává viditelnost projektů.
        var ownerId = await _fixture.EnsurePersonAsync("ApiSrchOwner");
        var deputyId = await _fixture.EnsurePersonAsync("ApiSrchDeputy");
        var projectId = await _fixture.EnsureProjectAsync("APISRCHDEP");
        var foreignProjectId = await _fixture.EnsureProjectAsync("APISRCHCIZI");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiSrchSub", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        var ownRecordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Zkousnihledani vlastni");
        var foreignRecordId = await _fixture.EnsureRecordAsync(foreignProjectId, ownerId, subsystemId, "U", "Zkousnihledani cizi");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Search/Suggest?q=Zkousnihledani&asUser={deputyId}");
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        json.Should().Contain($"\"zaznamId\":{ownRecordId}", "záznam vlastního projektu se najde");
        json.Should().NotContain($"\"zaznamId\":{foreignRecordId}", "cizí projekt filtruje ProjectVisibilityResolver");
    }

    [Fact]
    public async Task Suggest_UzivatelBezProjektu_VratiPrazdnyVysledek()
    {
        var loneId = await _fixture.EnsurePersonAsync("ApiSrchLone");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Search/Suggest?q=Zkousnihledani&asUser={loneId}");
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        json.Should().Contain("\"totalCount\":0", "kdo nevidí žádný projekt, nenajde nic — ale nedostane chybu");
    }

    [Fact]
    public async Task Index_ZastupceVedoucihoSubsystemu_StrankaSeVykresli()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiSrchIdxOwner");
        var deputyId = await _fixture.EnsurePersonAsync("ApiSrchIdxDeputy");
        var projectId = await _fixture.EnsureProjectAsync("APISRCHIDX");
        var subsystemId = await _fixture.EnsureSubsystemAsync("ApiSrchIdxSub", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Search?q=Zkousnihledani&asUser={deputyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
