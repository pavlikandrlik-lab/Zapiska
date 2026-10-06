using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Hlavička je standardní gov-header se skip-links a gov-navigation (spec 2026-09-23 §4.3).
/// Kotví se na atributy a ASCII — Razor kóduje diakritiku na entity.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class LayoutGovHeaderRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public LayoutGovHeaderRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetAsync(string path, int osobaId)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var separator = path.Contains('?') ? '&' : '?';
        var response = await client.GetAsync($"{path}{separator}asUser={osobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private static string NavSegment(string html)
    {
        var start = html.IndexOf("id=\"main-navigation\"", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "hlavní navigace musí mít id main-navigation (cíl skip-linku)");
        var end = html.IndexOf("</nav>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    [Fact]
    public async Task Hlavicka_JeGovHeader_SeSkipLinkyANavigaci()
    {
        var html = await GetAsync("/Projekty", _fixture.AdminOsobaId);

        html.Should().Contain("<div class=\"gov-skip-links\">");
        html.Should().Contain("href=\"#main-navigation\"");
        html.Should().Contain("href=\"#main\"");
        html.Should().Contain("<header class=\"gov-header\">");
        html.Should().Contain("class=\"gov-navigation app-main-nav\"");
        html.Should().Contain("js-gov-header__navigation-trigger", "hamburger pro úzký displej");
        html.Should().Contain("class=\"gov-search gov-search--fixed-width app-search\"",
            "vyhledávání zůstává v hlavičce");

        html.Should().NotContain("app-header");
        html.Should().NotContain("app-topbar");
        html.Should().NotContain("class=\"app-nav");
        html.Should().NotContain("data-user-menu");
        html.Should().NotContain("class=\"skip-link\"");
    }

    [Fact]
    public async Task AktivniPolozka_MaAriaCurrent_PraveJednou()
    {
        var nav = NavSegment(await GetAsync("/Projekty", _fixture.AdminOsobaId));

        Regex.Matches(nav, "aria-current=\"page\"").Should().HaveCount(1);
        nav.Should().Contain("<a href=\"/Projekty\" aria-current=\"page\">");
    }

    [Fact]
    public async Task Dashboard_NezvyraznujeZadnouPolozku()
    {
        var nav = NavSegment(await GetAsync("/", _fixture.AdminOsobaId));
        nav.Should().NotContain("aria-current");
    }

    [Fact]
    public async Task Navigace_SkryvaSekceBezOpravneni()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiNavOutsider");

        // Dokumentace má jen [Authorize] → otevře ji i osoba bez rolí.
        var outsiderNav = NavSegment(await GetAsync("/Dokumentace/Uzivatelska-prirucka", outsiderId));
        outsiderNav.Should().Contain("href=\"/Projekty\"");
        outsiderNav.Should().Contain("href=\"/Jednani\"");
        outsiderNav.Should().NotContain("href=\"/Osoby\"");
        outsiderNav.Should().NotContain("href=\"/Ciselniky\"");
        outsiderNav.Should().NotContain("href=\"/Nastaveni\"");

        var adminNav = NavSegment(await GetAsync("/Dokumentace/Uzivatelska-prirucka", _fixture.AdminOsobaId));
        adminNav.Should().Contain("href=\"/Osoby\"");
        adminNav.Should().Contain("href=\"/Ciselniky\"");
        adminNav.Should().Contain("href=\"/Nastaveni\"");
    }

    [Fact]
    public async Task MenuUzivatele_JeGovDropdown_SOdkazyNaProfil()
    {
        var html = await GetAsync("/Projekty", _fixture.AdminOsobaId);

        html.Should().Contain("<gov-dropdown position=\"right\" class=\"app-user-menu\">");
        html.Should().Contain("name=\"person-fill\"");
        html.Should().Contain("href=\"/Profil\"");
        html.Should().Contain("href=\"/Profil#moje-prava\"");
    }
}
