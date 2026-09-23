using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Owner filter (data-filter-key="vlastnik") musí zobrazovat „Příjmení Jméno" a řadit dle
/// příjmení (ne dle jména). Uživatelský požadavek 2026-07-05.
/// Seed: dva vlastníci, u nichž je pořadí dle jména opačné než dle příjmení, aby test
/// jednoznačně rozlišil obojí.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectOwnerFilterRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public ProjectOwnerFilterRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<int> SeedProjectWithTwoOwnersAsync()
    {
        // Příjmení pořadí: Adamcova < Zychova.  Jméno pořadí: Alena (Zychova) < Zaneta (Adamcova) → opačné.
        var ownerAdam = await _fixture.EnsurePersonAsync("ApiOwnerFiltAdam", jmeno: "Zaneta", prijmeni: "Adamcova");
        var ownerZych = await _fixture.EnsurePersonAsync("ApiOwnerFiltZych", jmeno: "Alena", prijmeni: "Zychova");

        var projectId = await _fixture.EnsureProjectAsync("APIOWNERFILT");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIOWNERFILTSUB", ownerZych);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerAdam);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerZych);
        await _fixture.EnsureRecordAsync(projectId, ownerAdam, subsystemId, "U", "owner filter record adam");
        await _fixture.EnsureRecordAsync(projectId, ownerZych, subsystemId, "U", "owner filter record zych");
        return projectId;
    }

    private async Task<string> GetOwnerSelectHtmlAsync(int projectId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var match = Regex.Match(
            html,
            "<select[^>]*data-filter-key=\"vlastnik\"[^>]*>(.*?)</select>",
            RegexOptions.Singleline);
        match.Success.Should().BeTrue("owner filter select musí být v renderovaném HTML.");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task OwnerFilter_RendersLabelSurnameFirst()
    {
        var projectId = await SeedProjectWithTwoOwnersAsync();
        var selectHtml = await GetOwnerSelectHtmlAsync(projectId);

        selectHtml.Should().Contain("Adamcova Zaneta", "label musi byt ve formatu Prijmeni Jmeno.");
        selectHtml.Should().Contain("Zychova Alena", "label musi byt ve formatu Prijmeni Jmeno.");
        selectHtml.Should().NotContain("Zaneta Adamcova", "stary format Jmeno Prijmeni nesmi zustat.");
        selectHtml.Should().NotContain("Alena Zychova", "stary format Jmeno Prijmeni nesmi zustat.");
    }

    [Fact]
    public async Task OwnerFilter_OrdersBySurname_NotFirstName()
    {
        var projectId = await SeedProjectWithTwoOwnersAsync();
        var selectHtml = await GetOwnerSelectHtmlAsync(projectId);

        var idxAdamcova = selectHtml.IndexOf("Adamcova Zaneta", StringComparison.Ordinal);
        var idxZychova = selectHtml.IndexOf("Zychova Alena", StringComparison.Ordinal);

        idxAdamcova.Should().BeGreaterThanOrEqualTo(0);
        idxZychova.Should().BeGreaterThanOrEqualTo(0);
        idxAdamcova.Should().BeLessThan(idxZychova,
            "vlastníci musí být řazeni dle příjmení (Adamcova před Zychova), ne dle jména (Alena před Zaneta).");
    }
}
