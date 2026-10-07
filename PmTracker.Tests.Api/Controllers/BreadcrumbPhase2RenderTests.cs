using System.Net;
using System.Text.Encodings.Web;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Fáze 2a: drobečková lišta na jednoduchých sekcích (ploché kořeny + Číselníky + podstránky Přehledu).
/// Labely s diakritikou Razor HTML-enkóduje (á→&#xE1;), proto porovnáváme přes HtmlEncoder.Default,
/// stejný enkodér jaký používá Razor.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class BreadcrumbPhase2RenderTests
{
    private readonly ApiSqlFixture _fixture;

    public BreadcrumbPhase2RenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Uživatel 2026-10-07: kořen sekce (zanoření 0) drobečkovou lištu nemá.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/Dashboard")]
    [InlineData("/Projekty")]
    [InlineData("/Osoby")]
    [InlineData("/Nastaveni")]
    [InlineData("/Profil")]
    [InlineData("/Jednani")]
    [InlineData("/Ciselniky")]
    [InlineData("/Dokumentace/Technicka/Strom-dokumentace")]
    public async Task SectionLanding_HasNoBreadcrumbBar(string path)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"{path}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("class=\"app-breadcrumb-bar\"");
    }

    [Fact]
    public async Task CiselnikDetail_RendersUnderCiselniky()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky/Detail/typy-ukolu?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("class=\"app-breadcrumb-bar\"");
        html.Should().Contain("app-breadcrumb-back");
        html.Should().Contain(HtmlEncoder.Default.Encode("Číselníky"));
    }

    [Theory]
    [InlineData("/Dashboard/Focus")]
    [InlineData("/Dashboard/News")]
    [InlineData("/Dashboard/Meetings")]
    public async Task DashboardSubpage_RendersUnderPrehled(string path)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"{path}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("app-breadcrumb-back");   // ← na rodiče (Přehled)
        html.Should().Contain("app-breadcrumb-close");  // podstránka = zavíratelná entita (aktuální = model.PageTitle)
        html.Should().Contain(HtmlEncoder.Default.Encode("Přehled"));
    }

    [Fact]
    public async Task DocumentationTree_HasNoBreadcrumbBar_AndDropsLocalBreadcrumb()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Dokumentace/Technicka/Strom-dokumentace?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("class=\"app-breadcrumb-bar\"", "strom dokumentace je kořen sekce");
        html.Should().NotContain("docs-breadcrumb");   // lokální breadcrumb nahrazen frame lištou
    }

    [Fact]
    public async Task DocumentationPage_RendersUnderDokumentace_AndDropsLocalBreadcrumb()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Dokumentace/Technicka/Architektura?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("app-breadcrumb-close");  // podstránka pod „Dokumentace"
        html.Should().Contain(HtmlEncoder.Default.Encode("Dokumentace"));
        html.Should().NotContain("docs-breadcrumb");
    }
}
