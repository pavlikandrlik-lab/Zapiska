using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Patička je standardní gov-footer se třemi sloupci jako dřív a řádkem verze
/// (spec 2026-09-23 §9.3, §12.7).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class LayoutGovFooterRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public LayoutGovFooterRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> FooterAsync()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().NotContain("app-footer");
        var start = html.IndexOf("<footer class=\"gov-footer\">", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        return html[start..html.IndexOf("</footer>", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task Paticka_MaOdkazyNaDokumentaci()
    {
        var footer = await FooterAsync();
        footer.Should().Contain("href=\"/Dokumentace/Uzivatelska-prirucka\"");
        footer.Should().Contain("href=\"/Dokumentace/Technicka/Strom-dokumentace\"");
        footer.Should().Contain("href=\"/Dokumentace/qa\"");
        footer.Should().Contain("href=\"/Dokumentace/Changelog\"");
    }

    [Fact]
    public async Task ExterniOdkazy_OteviraGovLinkExternal()
    {
        var footer = await FooterAsync();
        footer.Should().Contain("<gov-link href=\"https://servicedesk.fis.acr\" external size=\"s\">");
        footer.Should().Contain("href=\"https://www.fis.acr/fis\"");
        footer.Should().Contain("href=\"https://www.portalcechy.sis.acr\"");
    }

    [Fact]
    public async Task Paticka_MaKontaktyAVerzi()
    {
        var footer = await FooterAsync();
        footer.Should().Contain("<address class=\"app-address\">");
        footer.Should().Contain("973 200 840");
        footer.Should().Contain("DS gov.cz 4.7.0");
    }
}
