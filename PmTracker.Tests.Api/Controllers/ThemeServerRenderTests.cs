using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Server vypíše data-theme jen pro výslovnou volbu (cookie dark/light). Bez ní atribut
/// chybí a motiv dosadí podle systému inline skript v &lt;head&gt; (spec 2026-09-23 §12.4).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ThemeServerRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public ThemeServerRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> HtmlTagAsync(string? cookie)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        if (cookie is not null)
        {
            client.DefaultRequestHeaders.Add("Cookie", $"pmtracker.theme.mode={cookie}");
        }

        var response = await client.GetAsync($"/Projekty?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return Regex.Match(html, "<html[^>]*>").Value;
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task VyslovnaVolba_SeVykresliNaServeru(string theme)
    {
        var tag = await HtmlTagAsync(theme);
        tag.Should().Contain($"data-theme=\"{theme}\"");
        tag.Should().NotContain("data-theme-mode");
    }

    [Fact]
    public async Task BezCookie_AtributChybi()
    {
        (await HtmlTagAsync(null)).Should().NotContain("data-theme");
    }

    [Fact]
    public async Task CookieAuto_SeChovaJakoBezVolby()
    {
        // Pozůstatek dnešní 3stavové logiky. data-theme="auto" by přepnul jen gov tokeny.
        (await HtmlTagAsync("auto")).Should().NotContain("data-theme");
    }
}
