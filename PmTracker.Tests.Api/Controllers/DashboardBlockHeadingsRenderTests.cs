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

    [Theory]
    [InlineData("focus", DashboardBlockTitles.Focus)]
    [InlineData("meetings", DashboardBlockTitles.Meetings)]
    [InlineData("news", DashboardBlockTitles.News)]
    public async Task Index_BlockHasHeading_LabelledBy_AndHeadingPrecedesLazyContent(string block, string expectedTitle)
    {
        var html = await GetDecodedAsync("/dashboard");

        var headingId = $"dashboard-{block}-heading";
        html.Should().Contain($"<h2 class=\"dashboard-panel-heading\" id=\"{headingId}\">{expectedTitle}</h2>");

        var sectionStart = html.IndexOf($"data-dashboard-panel=\"{block}\"", StringComparison.Ordinal);
        sectionStart.Should().BeGreaterThan(0);
        var sectionTagStart = html.LastIndexOf("<section", sectionStart, StringComparison.Ordinal);
        var sectionTag = html[sectionTagStart..html.IndexOf('>', sectionStart)];
        sectionTag.Should().Contain($"aria-labelledby=\"{headingId}\"");

        var headingAt = html.IndexOf($"id=\"{headingId}\"", StringComparison.Ordinal);
        var placeholderAt = html.IndexOf("data-dashboard-panel-placeholder", sectionStart, StringComparison.Ordinal);
        var contentAt = html.IndexOf("data-dashboard-panel-content", sectionStart, StringComparison.Ordinal);
        headingAt.Should().BeGreaterThan(sectionStart).And.BeLessThan(placeholderAt,
            "nadpis je v kostře nad skeletonem — vidět i během načítání a při chybě načtení obsahu");
        placeholderAt.Should().BeLessThan(contentAt);
    }

    [Fact]
    public async Task Index_HasVisuallyHiddenH1()
    {
        var html = await GetDecodedAsync("/dashboard");

        html.Should().Contain("<h1 class=\"app-sr-only\">Přehled</h1>");
    }
}
