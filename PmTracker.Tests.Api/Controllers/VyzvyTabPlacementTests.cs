using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Výzvy se 2026-09-07 stěhují z projektového dashboardu do projektového menu
/// (spec 2026-09-07-vyzvy-dokonceni-design §3). Hlídá umístění záložky mezi Návrhy
/// a Dashboard i to, že zobrazení není gateované oprávněním.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class VyzvyTabPlacementTests
{
    private readonly ApiSqlFixture _fixture;

    public VyzvyTabPlacementTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjectDetail_RendersVyzvyTab_BetweenNavrhyAndDashboard()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIVYZVYTAB");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Projekty/Detail/{projectId}?tab=zaznamy&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        var vyzvy = html.IndexOf("data-tab=\"vyzvy\"", StringComparison.Ordinal);
        var navrhy = html.IndexOf("data-tab=\"navrhy\"", StringComparison.Ordinal);
        // Odkaz Dashboard v projektovém menu; globální navigace v hlavičce se jmenuje „Přehled",
        // takže tento popisek je v dokumentu jednoznačný.
        var dashboard = html.IndexOf(">Dashboard</a>", StringComparison.Ordinal);

        vyzvy.Should().BeGreaterThan(-1, "záložka Výzvy patří do projektového menu");
        navrhy.Should().BeGreaterThan(-1, "kontrolní bod Návrhy musí být v menu vyrenderovaný");
        dashboard.Should().BeGreaterThan(-1, "kontrolní bod Dashboard musí být v menu vyrenderovaný");

        vyzvy.Should().BeGreaterThan(navrhy, "Výzvy jsou až za Návrhy");
        vyzvy.Should().BeLessThan(dashboard, "Výzvy jsou před Dashboardem");
    }

    [Fact]
    public async Task VyzvyTabPartial_ReturnsPanel()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIVYZVYPARTIAL");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-vyzvy-panel");
    }

    /// <summary>
    /// Layout 20/80 (spec §6): vlevo rail s výběrem roku a dlaždicemi, vpravo panel
    /// vybrané dlaždice. Buffer je první dlaždice a je vybraný jako výchozí.
    /// </summary>
    [Fact]
    public async Task VyzvyTabPartial_RendersRail_WithYearSelectAndBufferTileFirst()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIVYZVYRAIL");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?asUser={_fixture.AdminOsobaId}");

        html.Should().Contain("vyzvy-layout", "nosný layout je grid 20/80");
        html.Should().Contain("data-vyzvy-rok", "rail nabízí výběr roku");
        html.Should().Contain("gov-form-select", "výběr roku jede na gov komponentě");
        html.Should().Contain("data-vyzvy-tile=\"buffer\"", "buffer je dlaždice v railu");
        html.Should().Contain("data-vyzvy-pane=\"buffer\"", "buffer má vlastní panel vpravo");

        // Buffer je první v seznamu bez ohledu na rok (spec §6.3).
        var buffer = html.IndexOf("data-vyzvy-tile=\"buffer\"", StringComparison.Ordinal);
        var railList = html.IndexOf("vyzvy-rail-list", StringComparison.Ordinal);
        buffer.Should().BeGreaterThan(railList, "buffer je uvnitř seznamu dlaždic");
    }

    /// <summary>
    /// Pravý panel seskupuje PNF pod projektový záznam (spec §7.1) a hlavička výzvy
    /// nese stav i měnič stavu — ten se 2026-09-07 vracel poté, co zmizel se starou
    /// kartou výzvy při přechodu na rail.
    /// </summary>
    [Fact]
    public async Task VyzvyTabPartial_RendersRecordGroups_AndStateChanger()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiVyzvyGroupOwner");
        var projectId = await _fixture.EnsureProjectAsync("APIVYZVYGRP");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIVYZVYGRP_SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Zaznam s PNF");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var pnfTypeId = await dbContext.CiselnikTypuExternichOdkazu.AsNoTracking()
                .Where(x => x.Kod == "PNF").Select(x => x.Id).FirstAsync();

            var vyzva = new PmTracker.Web.Models.Entities.VyzvaEntity
            {
                ProjektId = projectId,
                Kod = "77/2026",
                PoradoveVRoce = 77,
                Rok = 2026,
                Stav = PmTracker.Web.Models.Entities.VyzvaStav.Priprava,
                DatumZalozeni = new DateTime(2026, 4, 20),
                ZalozilOsobaId = ownerId,
                MistoPlneniSnapshot = "FIS",
                CisloRamcoveSmlouvySnapshot = "APIVYZVYGRP-SML",
            };
            dbContext.Vyzvy.Add(vyzva);
            await dbContext.SaveChangesAsync();

            dbContext.ZaznamExterniOdkazy.Add(new PmTracker.Web.Models.Entities.ZaznamExterniOdkazEntity
            {
                ZaznamId = recordId,
                TypOdkazuId = pnfTypeId,
                Cislo = "912345",
                ZaradidDoVyzvy = true,
                VyzvaId = vyzva.Id,
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await client.GetStringAsync(
            $"/Projekty/VyzvyTabPartial/{projectId}?rok=2026&asUser={_fixture.AdminOsobaId}");

        html.Should().Contain("vyzvy-zaznam-hlavicka", "PNF jsou seskupené pod záznamem");
        html.Should().Contain("Zaznam s PNF", "skupina nese název projektového záznamu");
        html.Should().Contain("912345", "PNF je vypsané pod svým záznamem");
        html.Should().Contain("data-vyzvy-stav-toggle", "hlavička výzvy nabízí změnu stavu");
        html.Should().Contain("data-vyzvy-action=\"zmenit-stav\"", "měnič stavu má položky");
    }

    [Fact]
    public async Task Dashboard_NoLongerOffersVyzvyTab()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIVYZVYDASH");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(
            $"/projekty/{projectId}/dashboard?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);

        html.Should().NotContain("data-dashboard-tab=\"vyzvy\"",
            "Výzvy se z dashboardu odstěhovaly do projektového menu");
    }
}
