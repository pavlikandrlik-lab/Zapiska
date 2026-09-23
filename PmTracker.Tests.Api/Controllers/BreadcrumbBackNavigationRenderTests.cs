using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// C1 (2026-07-10): šipka ← v breadcrumbs = origin (validovaný returnUrl) >
/// kanonická záložka entity > URL předposledního drobečku. Klik na projekt-drobeček
/// zůstává homepage projektu (bez tab).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class BreadcrumbBackNavigationRenderTests
{
    private readonly ApiSqlFixture _fixture;
    public BreadcrumbBackNavigationRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    private static string BackHref(string html)
    {
        var match = Regex.Match(html, "app-breadcrumb-back\" href=\"([^\"]+)\"");
        match.Success.Should().BeTrue("stránka má renderovat šipku ← v breadcrumb liště");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private async Task<string> GetHtmlAsync(string url)
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(url);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    private async Task<(int projectId, int meetingId, int recordId, int proposalId)> SeedAsync()
    {
        var leadId = await _fixture.EnsurePersonAsync("ApiCrumbBack");
        var projectId = await _fixture.EnsureProjectAsync("APICRUMBBK");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APICRUMBBK_SUB", leadId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, leadId);
        var meetingId = await _fixture.EnsureMeetingAsync(projectId, meetingNumber: 9810);
        var recordId = await _fixture.EnsureRecordAsync(projectId, leadId, subsystemId, "U", "ApiCrumbBackRec");
        var proposalId = await _fixture.EnsurePendingCreateProposalAsync(projectId, subsystemId, leadId);
        return (projectId, meetingId, recordId, proposalId);
    }

    [Fact]
    public async Task MeetingDetail_BackArrow_TargetsProjectMeetingsTab()
    {
        var (_, meetingId, _, _) = await SeedAsync();
        var html = await GetHtmlAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        BackHref(html).Should().Contain("tab=jednani", "kanonická záložka jednání");
    }

    [Fact]
    public async Task MeetingDetail_BackArrow_HonorsLocalReturnUrl()
    {
        var (_, meetingId, _, _) = await SeedAsync();
        var html = await GetHtmlAsync($"/Jednani/Detail/{meetingId}?returnUrl=%2FJednani&asUser={_fixture.AdminOsobaId}");
        BackHref(html).Should().Be("/Jednani", "origin (přehled jednání) má přednost");
    }

    [Fact]
    public async Task MeetingDetail_BackArrow_RejectsExternalReturnUrl()
    {
        var (_, meetingId, _, _) = await SeedAsync();
        var html = await GetHtmlAsync($"/Jednani/Detail/{meetingId}?returnUrl=https%3A%2F%2Fevil.example&asUser={_fixture.AdminOsobaId}");
        BackHref(html).Should().Contain("tab=jednani", "externí URL se zahazuje (open-redirect ochrana)");
    }

    [Fact]
    public async Task MeetingDetail_ProjectCrumbLink_StaysWithoutTab()
    {
        var (projectId, meetingId, _, _) = await SeedAsync();
        var html = await GetHtmlAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        var projectLink = Regex.Matches(html, "app-breadcrumb-link\" href=\"([^\"]+)\"")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .FirstOrDefault(href => href.Contains($"/Projekty/Detail/{projectId}"));
        projectLink.Should().NotBeNull("projekt-drobeček má být odkaz");
        projectLink.Should().NotContain("tab=", "klik na projekt = homepage projektu (Záznamy)");
    }

    [Fact]
    public async Task ProposalDetail_BackArrow_TargetsProposalsTab()
    {
        var (projectId, _, _, proposalId) = await SeedAsync();
        var html = await GetHtmlAsync($"/Navrhy/ProposalDetail?projektId={projectId}&proposalId={proposalId}&asUser={_fixture.AdminOsobaId}");
        BackHref(html).Should().Contain("tab=navrhy", "návrhové stránky patří do záložky Návrhy");
    }

    [Fact]
    public async Task RecordEdit_BackArrow_TargetsRecordsTab()
    {
        var (_, _, recordId, _) = await SeedAsync();
        var html = await GetHtmlAsync($"/Zaznamy/Edit/{recordId}?asUser={_fixture.AdminOsobaId}");
        BackHref(html).Should().Contain("tab=zaznamy", "kanonická záložka záznamů");
    }

    [Fact]
    public async Task RecordEdit_BackArrow_HonorsHarmonogramReturnUrl()
    {
        var (projectId, _, recordId, _) = await SeedAsync();
        var ret = Uri.EscapeDataString($"/Projekty/Detail/{projectId}?tab=harmonogram");
        var html = await GetHtmlAsync($"/Zaznamy/Edit/{recordId}?returnUrl={ret}&asUser={_fixture.AdminOsobaId}");
        BackHref(html).Should().Contain("tab=harmonogram", "cross-nav origin z gantu má přednost");
    }
}
