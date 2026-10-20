using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Pohled jako — zahájení, ukončení, platnost cookie a vykreslení menu a lišty.
/// Kotví na data-* atributy a ASCII jména (Razor kóduje diakritiku).
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ViewAsFlowTests
{
    private readonly ApiSqlFixture _fixture;

    public ViewAsFlowTests(ApiSqlFixture fixture) => _fixture = fixture;

    private HttpClient Client() => _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });

    private static HttpRequestMessage Request(HttpMethod method, string url, string? viewAsCookie = null, bool ajax = false, Dictionary<string, string>? form = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (viewAsCookie is not null) request.Headers.Add("Cookie", $"{ViewAsCookie.Name}={viewAsCookie}");
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        return request;
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    private static bool DeletesViewAsCookie(HttpResponseMessage response)
        => SetCookies(response).Any(v => v.StartsWith($"{ViewAsCookie.Name}=;", StringComparison.Ordinal) && v.Contains("1970"));

    private async Task<bool> AuditExistsAsync(string action, int actor, int target)
    {
        await using var db = _fixture.CreateDbContext();
        var entityId = target.ToString();
        return await db.AuthzAuditLog.AnyAsync(x =>
            x.EntityType == "pohled_jako" && x.Action == action && x.ActorOsobaId == actor && x.EntityId == entityId);
    }

    [Fact]
    public async Task Zahajit_Superadmin_SetsCookieAuditsAndRefreshesPage()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsStart");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Zahajit?asUser={_fixture.AdminOsobaId}",
            ajax: true, form: new() { ["OsobaId"] = target.ToString() }));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("refreshScope").GetString().Should().Be("page");
        SetCookies(response).Should().Contain(v => v.StartsWith($"{ViewAsCookie.Name}={_fixture.AdminOsobaId}-{target}", StringComparison.Ordinal));
        (await AuditExistsAsync("view_as_start", _fixture.AdminOsobaId, target)).Should().BeTrue();
    }

    [Fact]
    public async Task Zahajit_NonSuperadmin_Forbidden_NoCookie()
    {
        var plain = await _fixture.EnsurePersonAsync("ApiViewAsPlain");
        var target = await _fixture.EnsurePersonAsync("ApiViewAsPlainTarget");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Zahajit?asUser={plain}",
            ajax: true, form: new() { ["OsobaId"] = target.ToString() }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        SetCookies(response).Should().NotContain(v => v.StartsWith($"{ViewAsCookie.Name}=", StringComparison.Ordinal) && !v.Contains("1970"));
    }

    [Theory]
    [InlineData("self")]
    [InlineData("missing")]
    public async Task Zahajit_InvalidTarget_ReturnsFieldError(string kind)
    {
        var osobaId = kind == "self" ? _fixture.AdminOsobaId : int.MaxValue;
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Zahajit?asUser={_fixture.AdminOsobaId}",
            ajax: true, form: new() { ["OsobaId"] = osobaId.ToString() }));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("VIEW_AS_INVALID_TARGET");
        json.RootElement.GetProperty("fieldErrors").TryGetProperty("OsobaId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ActiveViewAs_PageShowsBarAndTargetIdentity_HidesStartItemAndAdminNav()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsCil");
        using var client = Client();

        var baseline = await (await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}"))).Content.ReadAsStringAsync();
        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}"));
        var html = await response.Content.ReadAsStringAsync();

        baseline.Should().Contain("data-view-as-start", "superadmin bez pohledu má v menu Zobrazit jako");
        baseline.Should().Contain("data-modal-url=\"/PohledJako/Vyber\"", "atribut na gov-buttonu se nesmí ztratit (Razor gotcha)");
        baseline.Should().Contain("href=\"/Nastaveni\"");
        baseline.Should().NotContain("data-view-as-bar");

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-view-as-bar");
        html.Should().Contain("ApiViewAsCil", "hlavička i lišta ukazují cílového uživatele");
        html.Should().NotContain("data-view-as-start", "během pohledu se nový pohled nezahajuje");
        html.Should().NotContain("href=\"/Nastaveni\"", "cílový uživatel nemá práva nastavení");
    }

    [Fact]
    public async Task ActiveViewAs_ProjectVisibilityFollowsTarget()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsVis");
        var visibleProject = await _fixture.EnsureProjectAsync("APIVAVIS1");
        var hiddenProject = await _fixture.EnsureProjectAsync("APIVAVIS2");
        await _fixture.EnsureProjectRoleAssignmentAsync(visibleProject, target, "PROJ_MAN");
        using var client = Client();

        var html = await (await client.SendAsync(Request(HttpMethod.Get, $"/Projekty?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}")))
            .Content.ReadAsStringAsync();

        // Uzavírací uvozovka: id 12 nesmí matchnout /Projekty/Detail/123 (karta má data-href="…").
        html.Should().Contain($"/Projekty/Detail/{visibleProject}\"");
        html.Should().NotContain($"/Projekty/Detail/{hiddenProject}\"", "superadmin by ho viděl, cílový uživatel ne");
    }

    [Fact]
    public async Task ActiveViewAs_ProfileShowsTargetRoles()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsRoles");
        var project = await _fixture.EnsureProjectAsync("APIVAROLE");
        await _fixture.EnsureProjectRoleAssignmentAsync(project, target, "PROJ_MAN");
        using var client = Client();

        var html = await (await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}")))
            .Content.ReadAsStringAsync();

        html.Should().Contain("PROJ_MAN", "Moje práva v pohledu jsou práva cílového uživatele");
    }

    [Fact]
    public async Task Ukoncit_DeletesCookie_AuditsAndReturnsToReferer()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsStop");
        using var client = Client();
        var request = Request(HttpMethod.Post, $"/PohledJako/Ukoncit?asUser={_fixture.AdminOsobaId}", $"{_fixture.AdminOsobaId}-{target}",
            form: new());
        request.Headers.Referrer = new Uri($"http://localhost/Projekty?asUser={_fixture.AdminOsobaId}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.Should().Be($"/Projekty?asUser={_fixture.AdminOsobaId}");
        DeletesViewAsCookie(response).Should().BeTrue();
        (await AuditExistsAsync("view_as_stop", _fixture.AdminOsobaId, target)).Should().BeTrue();
    }

    [Fact]
    public async Task Ukoncit_WithoutActiveViewAs_StillRedirects()
    {
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/PohledJako/Ukoncit?asUser={_fixture.AdminOsobaId}", form: new()));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Redirect, HttpStatusCode.Found);
        DeletesViewAsCookie(response).Should().BeTrue();
    }

    [Fact]
    public async Task CookieFromNonSuperadmin_IsIgnoredAndDeleted()
    {
        var plain = await _fixture.EnsurePersonAsync("ApiViewAsPlain2");
        var target = await _fixture.EnsurePersonAsync("ApiViewAsPlain2Target");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={plain}", $"{plain}-{target}"));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().NotContain("data-view-as-bar");
        html.Should().NotContain("ApiViewAsPlain2Target");
        DeletesViewAsCookie(response).Should().BeTrue();
    }

    [Fact]
    public async Task CookieOfAnotherRealUser_IsIgnoredAndDeleted()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsForeign");
        var otherAdmin = await _fixture.EnsurePersonAsync("ApiViewAsOtherAdmin");
        await using (var db = _fixture.CreateDbContext())
        {
            if (!await db.AuthzSuperadmins.AnyAsync(x => x.OsobaId == otherAdmin))
            {
                db.AuthzSuperadmins.Add(new AuthzSuperadminEntity { OsobaId = otherAdmin, CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
        }
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Profil?asUser={_fixture.AdminOsobaId}", $"{otherAdmin}-{target}"));
        var html = await response.Content.ReadAsStringAsync();

        html.Should().NotContain("data-view-as-bar");
        DeletesViewAsCookie(response).Should().BeTrue();
    }

    [Fact]
    public async Task Vyber_Superadmin_RendersModalWithPicker_NonAdminForbidden()
    {
        var plain = await _fixture.EnsurePersonAsync("ApiViewAsModalPlain");
        using var client = Client();

        var ok = await client.SendAsync(Request(HttpMethod.Get, $"/PohledJako/Vyber?asUser={_fixture.AdminOsobaId}", ajax: true));
        var html = await ok.Content.ReadAsStringAsync();
        var forbidden = await client.SendAsync(Request(HttpMethod.Get, $"/PohledJako/Vyber?asUser={plain}", ajax: true));

        ok.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-view-as-modal");
        html.Should().Contain("data-person-picker=\"single\"");
        html.Should().Contain("action=\"/PohledJako/Zahajit");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HledatOsoby_ReturnsCandidates_WithoutCurrentUser()
    {
        var target = await _fixture.EnsurePersonAsync("ApiViewAsSearchable");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/PohledJako/HledatOsoby?q=ApiViewAsSearchable&asUser={_fixture.AdminOsobaId}", ajax: true));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var ids = json.RootElement.GetProperty("results").EnumerateArray().Select(x => x.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain(target);
        ids.Should().NotContain(_fixture.AdminOsobaId);
    }
}
