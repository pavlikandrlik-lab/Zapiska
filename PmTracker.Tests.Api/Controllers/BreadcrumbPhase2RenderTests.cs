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

    [Theory]
    [InlineData("/Osoby", "Osoby")]
    [InlineData("/Nastaveni", "Nastavení")]
    [InlineData("/Profil", "Můj profil")]
    [InlineData("/Jednani", "Jednání")]
    [InlineData("/Dashboard", "Přehled")]
    public async Task SectionLanding_RendersRootBreadcrumb(string path, string label)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"{path}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("aria-current=\"page\"");
        html.Should().Contain(HtmlEncoder.Default.Encode(label));
    }

    [Fact]
    public async Task CiselnikyIndex_RendersRootCrumb()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Ciselniky?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
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
    public async Task DocumentationTree_RendersRootCrumb_AndDropsLocalBreadcrumb()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Dokumentace/Technicka/Strom-dokumentace?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain(HtmlEncoder.Default.Encode("Dokumentace"));
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
