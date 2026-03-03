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

    [Fact]
    public async Task TechnicalDocumentation_ShouldRenderStandaloneDocumentationBreadcrumb()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Dokumentace/Technicka-dokumentace?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("docs-breadcrumb");
        html.Should().Contain("href=\"/Dokumentace/Technicka-dokumentace\">Dokumentace</a>");
        html.Should().Contain("Technick&#xE1; dokumentace");
    }

    [Fact]
    public async Task Layout_ShouldNotRenderThemeCycleMenuAction()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("Vzhled: přepnout");
    }
}
