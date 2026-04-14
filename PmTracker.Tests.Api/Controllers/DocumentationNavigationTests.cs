using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class DocumentationNavigationTests
{
    private readonly ApiSqlFixture _fixture;

    public DocumentationNavigationTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/Dokumentace/Technicka/Strom-dokumentace", "Strom dokumentace")]
    [InlineData("/Dokumentace/Technicka/Systemovy-kontext", "Systémový kontext")]
    [InlineData("/Dokumentace/Technicka/Architektura", "Architektura")]
    [InlineData("/Dokumentace/Technicka/Runtime-konfigurace", "Runtime konfigurace")]
    [InlineData("/Dokumentace/Technicka/Instalace-a-deployment-iis", "Instalace a deployment")]
    [InlineData("/Dokumentace/Technicka/Web-server-iis", "Konfigurace web serveru IIS")]
    [InlineData("/Dokumentace/Technicka/Databaze-bootstrap-a-migrace", "Databáze")]
    [InlineData("/Dokumentace/Technicka/Bezpecnost-a-opravneni", "Bezpečnost a autorizace")]
    [InlineData("/Dokumentace/Technicka/Provozni-runbooky", "Provozní runbooky")]
    [InlineData("/Dokumentace/Technicka/Testovani-a-kvalita", "Testování a kvalita")]
    [InlineData("/Dokumentace/Technicka/Troubleshooting-a-recovery", "Troubleshooting a recovery")]
    [InlineData("/Dokumentace/Uzivatelska-prirucka", "Uživatelská příručka")]
    [InlineData("/Dokumentace/qa", "Q and A")]
    [InlineData("/Dokumentace/Changelog", "Changelog verzí")]
    public async Task DocumentationRoutes_ShouldReturnSuccessAndRenderNavigation(string route, string expectedMarker)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"{route}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("docs-breadcrumb");
        html.Should().Contain("Technická dokumentace");
        html.Should().Contain("Uživatelské a provozní");
        html.Should().Contain(expectedMarker);
    }

    [Fact]
    public async Task Layout_ShouldContainUpdatedTechnicalDocumentationLink()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("href=\"/Dokumentace/Technicka/Strom-dokumentace\"", "footer odkaz musí vést na kořen technické dokumentace");
    }

    [Fact]
    public async Task Layout_ShouldNotRenderThemeCycleMenuAction()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("Vzhled: přepnout");
        html.Should().Contain("class=\"gov-switch gov-theme-switch app-theme-switch\"", "layout má renderovat gov theme switch markup");
        html.Should().Contain("aria-label=\"Přepínač barevného schématu stránky\"");
        html.Should().Contain("data-theme-switch-input");
    }

    [Fact]
    public async Task Layout_ShouldRenderServerThemeAttributesFromCookie()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", "pmtracker.theme.mode=dark");

        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-theme=\"dark\"");
        html.Should().Contain("data-theme-mode=\"dark\"");
    }
}
