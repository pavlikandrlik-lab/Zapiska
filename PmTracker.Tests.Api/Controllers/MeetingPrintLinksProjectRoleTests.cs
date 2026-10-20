using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// 2026-10-20: PM / vedoucí subsystému bez globální role dostával u tisku jednání ze seznamu
/// Jednání a ze záložky projektu „Přístup odepřen“ — odkazy nenesly projektId, policy
/// export.pdf.jednani / export.word.jednani pak udělala globální kontrolu. Testy jdou cestou
/// uživatele: vykreslí stránku jako projektová role, vezmou odkaz z HTML a otevřou ho.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class MeetingPrintLinksProjectRoleTests
{
    private readonly ApiSqlFixture _fixture;

    public MeetingPrintLinksProjectRoleTests(ApiSqlFixture fixture) => _fixture = fixture;

    /// <summary>Osoba jen s projektovou / subsystémovou rolí (žádná globální), vlastní projekt a jednání.</summary>
    private async Task<(int OsobaId, int ProjectId, int MeetingId)> SeedAsync(string role)
    {
        var marker = $"APIPRN{role.ToUpperInvariant()}";
        var osobaId = await _fixture.EnsurePersonAsync($"ApiPrint{role}");
        var projectId = await _fixture.EnsureProjectAsync(marker);
        if (role.Contains("pm", StringComparison.Ordinal))
        {
            await _fixture.EnsureProjectRoleAssignmentAsync(projectId, osobaId, ProjectRoleCodes.ProjectManager);
        }

        if (role.Contains("lead", StringComparison.Ordinal))
        {
            var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", osobaId);
            await _fixture.EnsureSubsystemLeadAsync(projectId, subsystemId, osobaId);
        }

        var meetingId = await _fixture.EnsureMeetingAsync(projectId, 9601);
        return (osobaId, projectId, meetingId);
    }

    private static IReadOnlyList<string> PrintLinks(string html, int meetingId) =>
        Regex.Matches(WebUtility.HtmlDecode(html), $@"/Export/Jednani/{meetingId}/(?:Tisk|Word)[^""'\s<>]*")
            .Select(m => m.Value)
            .Distinct()
            .ToList();

    private static int? ProjektIdOf(string link)
    {
        var match = Regex.Match(link, @"[?&]projektId=(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static string WithAsUser(string url, int osobaId) =>
        url.Contains('?') ? $"{url}&asUser={osobaId}" : $"{url}?asUser={osobaId}";

    private static async Task<string> GetOkHtmlAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        return html;
    }

    [Theory]
    [InlineData("pm", "seznam")]
    [InlineData("lead", "seznam")]
    [InlineData("pmlead", "seznam")]
    [InlineData("pm", "zalozka")]
    [InlineData("lead", "zalozka")]
    [InlineData("pmlead", "zalozka")]
    public async Task PrintLinks_OpenForProjectRoleWithoutGlobalRole(string role, string page)
    {
        var (osobaId, projectId, meetingId) = await SeedAsync(role);
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var pageUrl = page == "seznam"
            ? $"/Jednani?projektId={projectId}&asUser={osobaId}"
            : $"/Projekty/Detail/{projectId}?tab=jednani&asUser={osobaId}";

        var links = PrintLinks(await GetOkHtmlAsync(client, pageUrl), meetingId);

        links.Should().HaveCount(2, "karta jednání nabízí tisk (PDF) i Word");
        foreach (var link in links)
        {
            var response = await client.GetAsync(WithAsUser(link, osobaId));
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"role {role} otevírá {link} ze stránky {page}");
            ProjektIdOf(link).Should().Be(projectId, "policy export.*.jednani čte projekt jen z requestu");
        }
    }

    [Fact]
    public async Task PrintLinks_InMeetingList_CarryProjectOfEachMeeting_IncludingOlderYears()
    {
        var osobaId = await _fixture.EnsurePersonAsync("ApiPrintMulti");
        var projectA = await _fixture.EnsureProjectAsync("APIPRNMULTA");
        var projectB = await _fixture.EnsureProjectAsync("APIPRNMULTB");
        await _fixture.EnsureProjectRoleAssignmentAsync(projectA, osobaId, ProjectRoleCodes.ProjectManager);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectB, osobaId, ProjectRoleCodes.ProjectManager);
        var meetingA = await _fixture.EnsureMeetingAsync(projectA, 9611);
        var olderMeetingA = await _fixture.EnsureMeetingAsync(projectA, 9612);
        var meetingB = await _fixture.EnsureMeetingAsync(projectB, 9613);
        await using (var db = _fixture.CreateDbContext())
        {
            var older = await db.Jednani.FirstAsync(x => x.Id == olderMeetingA);
            older.DatumPlanovane = new DateTime(DateTime.Today.Year - 1, 11, 17);
            await db.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var html = await GetOkHtmlAsync(client, $"/Jednani?asUser={osobaId}");

        foreach (var (meetingId, projectId) in new[] { (meetingA, projectA), (olderMeetingA, projectA), (meetingB, projectB) })
        {
            var links = PrintLinks(html, meetingId);
            links.Should().HaveCount(2, $"jednání {meetingId} má na kartě tisk i Word");
            links.Should().OnlyContain(link => ProjektIdOf(link) == projectId,
                $"odkazy jednání {meetingId} musí nést jeho vlastní projekt {projectId}");
        }
    }
}
