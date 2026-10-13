using System.Net;
using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Editor návrhu založení potřebuje jen typ tiketu (2026-10-13). Dřív volal /ExterniOdkaz/Sync
/// (klíč externiodkazy.sync), takže zástupci vedoucího subsystému hlásil u existujícího
/// tiketu „Tiket se nepodařilo dohledat".
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ExterniOdkazTypTests
{
    private readonly ApiSqlFixture _fixture;

    public ExterniOdkazTypTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<(int ProjectId, int DeputyId, int OutsiderId)> SeedAsync(string marker)
    {
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var deputyId = await _fixture.EnsurePersonAsync($"{marker}Deputy");
        var outsiderId = await _fixture.EnsurePersonAsync($"{marker}Outsider");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureSubsystemRoleAsync(projectId, subsystemId, deputyId, SubsystemRoleCodes.DeputyLead);
        return (projectId, deputyId, outsiderId);
    }

    [Fact]
    public async Task Typ_ZastupceSNavrhemZalozeni_DostaneNormalizovanyTyp()
    {
        var (projectId, deputyId, _) = await SeedAsync("ApiTyp1");
        _fixture.ServiceDesk.Tikety["816001"] = new HotZaznamDto("816001", "PNF  ", "Strucne", "Popis tiketu");

        using var client = _fixture.ServiceDeskFactory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/ExterniOdkaz/Typ?cislo=816001&projektId={projectId}&asUser={deputyId}");
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        json.Should().Contain("\"nalezeno\":true").And.Contain("\"typ\":\"PNF\"", "CHAR(5) padding se ořízne");
        json.Should().Contain("\"popis\":\"Popis tiketu\"", "PNF předvyplňuje text požadavku");
        json.Should().NotContain("vyjadreni", "endpoint nevrací obsah tiketu ani harvest");
    }

    [Fact]
    public async Task Typ_BezKlicu_Vraci403()
    {
        var (projectId, _, outsiderId) = await SeedAsync("ApiTyp2");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/ExterniOdkaz/Typ?cislo=816002&projektId={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Typ_NeplatneCislo_Vraci400()
    {
        var (projectId, deputyId, _) = await SeedAsync("ApiTyp3");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/ExterniOdkaz/Typ?cislo=12ab&projektId={projectId}&asUser={deputyId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
