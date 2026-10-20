using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Services.Dashboard;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-10-20 Přehled — nadpisy bloků. Texty z jednoho zdroje (DashboardBlockTitles),
/// nadpisy v kostře stránky. Český text přes HtmlDecode (Razor kóduje diakritiku).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class DashboardBlockHeadingsRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public DashboardBlockHeadingsRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private async Task<string> GetDecodedAsync(string path)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var separator = path.Contains('?') ? '&' : '?';
        var response = await client.GetAsync($"{path}{separator}asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return WebUtility.HtmlDecode(html);
    }

    [Theory]
    [InlineData("/dashboard/focus", DashboardBlockTitles.Focus)]
    [InlineData("/dashboard/meetings", DashboardBlockTitles.Meetings)]
    [InlineData("/dashboard/news", DashboardBlockTitles.News)]
    public async Task DetailPage_TitleComesFromSharedBlockTitles(string path, string expectedTitle)
    {
        var html = await GetDecodedAsync(path);

        html.Should().Contain($"<title>{expectedTitle} - Zápiska</title>", "titulek detailní stránky = název bloku na Přehledu");
    }
}
