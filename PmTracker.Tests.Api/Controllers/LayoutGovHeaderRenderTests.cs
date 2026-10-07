using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

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
        html.Should().Contain("class=\"gov-header__content app-header-wide\"", "odchylka č. 7");
        html.Should().Contain("class=\"gov-navigation app-main-nav\"");
        html.Should().Contain("js-gov-header__navigation-trigger", "hamburger pro úzký displej");
        html.Should().Contain("class=\"gov-search app-search\"",
            "vyhledávání zůstává v hlavičce");

        html.Should().NotMatchRegex(@"app-header(?![\w-])", "stará třída hlavičky (app-header-wide je odchylka č. 7)");
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

    [Theory]
    [InlineData("/")]
    [InlineData("/Dashboard/Focus")]
    public async Task Prehled_JeZvyraznenyNaHlavniStranceIPodstrankach(string path)
    {
        var nav = NavSegment(await GetAsync(path, _fixture.AdminOsobaId));

        Regex.Matches(nav, "aria-current=\"page\"").Should().HaveCount(1);
        nav.Should().Contain("<a href=\"/dashboard\" aria-current=\"page\">",
            "Přehled se zvýrazňuje jako ostatní sekce (uživatel 2026-10-07)");
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

    [Fact]
    public async Task MenuUzivatele_ZobrazujeKodOrganizace()
    {
        var osobaId = await EnsurePersonInOrgUnitAsync("ApiHeaderOrgCode", orgUnitKod: "APIORG");

        var (label, name) = UserAccountTexts(await GetAsync("/Projekty", osobaId));

        // \s pokryje i nezlomitelnou mezeru kolem pomlčky.
        name.Should().MatchRegex(@"^Ing\. ApiHeaderOrgCode Api\s–\sAPIORG$");
        label.Should().MatchRegex(@"ApiHeaderOrgCode Api\s–\sAPIORG$");
    }

    [Fact]
    public async Task MenuUzivatele_BezOrganizacnihoCelku_NemaOsamocenouPomlcku()
    {
        var osobaId = await EnsurePersonInOrgUnitAsync("ApiHeaderNoOrg", orgUnitKod: null);

        var (label, name) = UserAccountTexts(await GetAsync("/Projekty", osobaId));

        name.Should().Be("Ing. ApiHeaderNoOrg Api");
        label.Should().EndWith("ApiHeaderNoOrg Api");
    }

    /// <summary>Text tlačítka účtu a jeho aria-label, s dekódovanými HTML entitami.</summary>
    private static (string Label, string Name) UserAccountTexts(string html)
    {
        var start = html.IndexOf("<gov-dropdown position=\"right\" class=\"app-user-menu\">", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "menu účtu má být gov-dropdown");
        var end = html.IndexOf("<ul slot=\"list\"", start, StringComparison.Ordinal);
        var button = html[start..end];

        var label = Regex.Match(button, "aria-label=\"([^\"]*)\"");
        var name = Regex.Match(button, "<span class=\"app-user-name\">([^<]*)</span>");
        label.Success.Should().BeTrue("tlačítko účtu má mít aria-label");
        name.Success.Should().BeTrue("tlačítko účtu má vypsat jméno ve span.app-user-name");

        return (WebUtility.HtmlDecode(label.Groups[1].Value), WebUtility.HtmlDecode(name.Groups[1].Value));
    }

    private async Task<int> EnsurePersonInOrgUnitAsync(string marker, string? orgUnitKod)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var email = $"{marker.ToLowerInvariant()}@pmtracker.test";
        var existing = await dbContext.Osoby.Where(x => x.Email == email).Select(x => (int?)x.Id).FirstOrDefaultAsync();
        if (existing.HasValue)
        {
            return existing.Value;
        }

        int? orgUnitId = null;
        if (orgUnitKod is not null)
        {
            var orgUnit = await dbContext.CiselnikOrganizacniCelky.FirstOrDefaultAsync(x => x.Kod == orgUnitKod);
            if (orgUnit is null)
            {
                orgUnit = new CiselnikOrganizacniCelekEntity { Kod = orgUnitKod, Nazev = $"{orgUnitKod} útvar" };
                dbContext.CiselnikOrganizacniCelky.Add(orgUnit);
                await dbContext.SaveChangesAsync();
            }

            orgUnitId = orgUnit.Id;
        }

        var person = new OsobaEntity
        {
            Jmeno = marker,
            Prijmeni = "Api",
            Titul = "Ing.",
            Email = email,
            OrganizaceId = await dbContext.CiselnikOrganizace.Select(x => x.Id).FirstAsync(),
            OrganizacniCelekId = orgUnitId
        };
        dbContext.Osoby.Add(person);
        await dbContext.SaveChangesAsync();
        return person.Id;
    }
}
