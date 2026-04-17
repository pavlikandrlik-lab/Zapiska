using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class GlobalSearchLayoutTests
{
    private readonly ApiSqlFixture _fixture;

    public GlobalSearchLayoutTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Layout_ShouldRenderGlobalSearchFormInHeader()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-global-search", "search form musí být vždy v layoutu");
        html.Should().Contain("gov-form-search", "kořenový kontejner musí být gov-form-search dle designsystem.gov.cz");
        html.Should().Contain("role=\"search\"", "form musí mít semantickou roli search");
        html.Should().Contain("name=\"q\"", "input pro dotaz musí být pojmenovaný q");
        html.Should().Contain("action=\"/Search", "form musí odkazovat na /Search");
    }

    [Fact]
    public async Task Layout_ShouldRenderSearchSubmitButton()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("slot=\"button\"", "submit je ve slotu button dle gov-form-search");
        html.Should().Contain("class=\"gov-button\"", "tlačítko používá gov-button třídu");
        html.Should().Contain("type=\"submit\"", "tlačítko musí být submit");
        html.Should().Contain("aria-label=\"Vyhledat\"", "tlačítko má přístupný popis");
    }

    [Fact]
    public async Task Layout_ShouldRenderSearchInputWithAccessibleLabel()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("slot=\"input\"", "input je ve slotu input dle gov-form-search");
        html.Should().Contain("gov-form-input__input", "input musí mít BEM třídu gov-form-input__input");
        html.Should().Contain("type=\"search\"", "input musí být typu search");
        html.Should().Contain("aria-label=\"Globální vyhledávání\"", "input musí mít přístupný popis");
    }

    [Fact]
    public async Task SearchIndex_ShouldReturnSuccessForEmptyQuery()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Search?asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SearchIndex_ShouldReturnSuccessForNonEmptyQuery()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Search?q=test&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
