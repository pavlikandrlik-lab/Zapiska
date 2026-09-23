using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Napojení panelu Výzev na server. Panel posílá akce ajaxem přes panelController.js,
/// takže potřebuje k dispozici antiforgery token — stránka projektu žádný formulář nemá
/// a nevygeneruje ho (2026-09-08: každý přesun PNF končil HTTP 400).
///
/// Antiforgery samotné se tady ověřit nedá: PmTrackerWebAppFactory vyměňuje IAntiforgery
/// za NoOpAntiforgery, takže by prošel i POST bez tokenu. Hlídá se proto to, co hlídat jde —
/// že panel token opravdu vykreslí a JS ho má kde najít.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvyPanelWiringTests
{
    private readonly ApiSqlFixture _fixture;

    public VyzvyPanelWiringTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task VyzvyTabPartial_VykresliAntiforgeryToken()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIVYZWIRE1");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?asUser={_fixture.AdminOsobaId}");

        html.Should().Contain("data-vyzvy-antiforgery",
            "panel si veze vlastní token — JS ho hledá nejdřív uvnitř panelu");
        html.Should().Contain("__RequestVerificationToken",
            "bez tokenu vrátí /vyzvy/prerdit i /vyzvy/zmenit-stav HTTP 400");
    }

    /// <summary>
    /// Token musí přežít i překreslení panelu po akci (reloadPanel nahrazuje celý element),
    /// jinak by druhý přesun v řadě zase spadl na 400.
    /// </summary>
    [Fact]
    public async Task VyzvyTabPartial_TokenJeUvnitrPanelu_NeMimoNej()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIVYZWIRE2");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?asUser={_fixture.AdminOsobaId}");

        var panel = html.IndexOf("data-vyzvy-panel", StringComparison.Ordinal);
        var token = html.IndexOf("data-vyzvy-antiforgery", StringComparison.Ordinal);

        panel.Should().BeGreaterThan(-1, "panel musí být vyrenderovaný");
        token.Should().BeGreaterThan(panel,
            "token je uvnitř panelu, aby ho reloadPanel vyměnil spolu s ním");
    }
}
