using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security.ViewAs;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>Pohled jako je jen pro čtení: blokace zápisu, zámek karty, keep-alive, sweep stránek.</summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class ViewAsReadOnlyTests
{
    private readonly ApiSqlFixture _fixture;

    public ViewAsReadOnlyTests(ApiSqlFixture fixture) => _fixture = fixture;

    private sealed record Scenario(int TargetId, int ProjectId, int RecordId, string Cookie);

    private async Task<Scenario> SeedAsync(string marker)
    {
        var targetId = await _fixture.EnsurePersonAsync($"{marker}Cil");
        var ownerId = await _fixture.EnsurePersonAsync($"{marker}Owner");
        var projectId = await _fixture.EnsureProjectAsync(marker.ToUpperInvariant());
        var subsystemId = await _fixture.EnsureSubsystemAsync($"{marker}S", ownerId);
        await _fixture.EnsureProjectRoleAssignmentAsync(projectId, targetId, "PROJ_MAN");
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", $"{marker} zaznam");
        return new Scenario(targetId, projectId, recordId, $"{_fixture.AdminOsobaId}-{targetId}");
    }

    private HttpClient Client() => _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });

    private static HttpRequestMessage Request(HttpMethod method, string url, string cookie, bool ajax = false, Dictionary<string, string>? form = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"{ViewAsCookie.Name}={cookie}");
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        if (form is not null) request.Content = new FormUrlEncodedContent(form);
        return request;
    }

    [Fact]
    public async Task AjaxPost_IsBlockedWithReadableMessage_AndNothingIsWritten()
    {
        var s = await SeedAsync("ApiVaRo1");
        using var client = Client();
        const string marker = "APIVARO1NEW";

        var response = await client.SendAsync(Request(HttpMethod.Post, $"/Projekty/SaveProject?asUser={_fixture.AdminOsobaId}", s.Cookie,
            ajax: true, form: new() { ["Zkratka"] = marker, ["CelyNazev"] = marker }));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("ok").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("VIEW_AS_READ_ONLY");
        await using var db = _fixture.CreateDbContext();
        (await db.Projekty.AnyAsync(x => x.Zkratka == marker)).Should().BeFalse();
    }

    [Fact]
    public async Task FormPost_RedirectsBackWithTempData()
    {
        var s = await SeedAsync("ApiVaRo2");
        using var client = Client();
        var request = Request(HttpMethod.Post, $"/Jednani/SaveStatus?asUser={_fixture.AdminOsobaId}", s.Cookie, form: new());
        request.Headers.Referrer = new Uri($"http://localhost/Jednani?asUser={_fixture.AdminOsobaId}");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        response.Headers.Location!.OriginalString.Should().Be($"/Jednani?asUser={_fixture.AdminOsobaId}");
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.StartsWith(".AspNetCore.Mvc.CookieTempDataProvider=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReleaseEditLockBeacon_IsBlocked_AndForeignLockSurvives()
    {
        var s = await SeedAsync("ApiVaRo3");
        await using (var db = _fixture.CreateDbContext())
        {
            db.ZaznamEditZamky.Add(new ZaznamEditZamekEntity
            {
                ZaznamId = s.RecordId, OsobaId = s.TargetId, ZiskanoAt = DateTime.UtcNow, HeartbeatAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        using var client = Client();
        var request = Request(HttpMethod.Post, $"/Zaznamy/ReleaseEditLock?asUser={_fixture.AdminOsobaId}", s.Cookie);
        request.Content = new StringContent($"{{\"zaznamId\":{s.RecordId}}}", System.Text.Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var check = _fixture.CreateDbContext();
        (await check.ZaznamEditZamky.AnyAsync(x => x.ZaznamId == s.RecordId && x.OsobaId == s.TargetId))
            .Should().BeTrue("admin nesmí cílovému uživateli uvolnit jeho zámek");
    }

    [Fact]
    public async Task MainPages_RenderForTarget_WithoutHittingWriteGuard()
    {
        var s = await SeedAsync("ApiVaRo4");
        using var client = Client();
        var admin = _fixture.AdminOsobaId;
        string[] urls =
        [
            $"/Dashboard?asUser={admin}",
            $"/Projekty?asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=zaznamy&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=harmonogram&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=jednani&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=tym&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=navrhy&asUser={admin}",
            $"/Projekty/Detail/{s.ProjectId}?tab=vyzvy&asUser={admin}",
            $"/Zaznamy/Detail/{s.RecordId}?asUser={admin}",
            $"/Zaznamy/Edit?id={s.RecordId}&asUser={admin}",
            $"/Jednani?asUser={admin}",
            $"/Profil?asUser={admin}",
            $"/Search?q=ApiVaRo4&asUser={admin}"
        ];

        foreach (var url in urls)
        {
            var response = await client.SendAsync(Request(HttpMethod.Get, url, s.Cookie));
            var body = await response.Content.ReadAsStringAsync();

            body.Should().NotContain("VIEW_AS_READ_ONLY", $"{url} nesmí při čtení zapisovat do DB");
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {body[..Math.Min(body.Length, 400)]}");
        }
    }

    [Fact]
    public async Task Editor_InViewAs_OpensWithoutCreatingLock()
    {
        var s = await SeedAsync("ApiVaRo5");
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}", s.Cookie));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-record-editor-form=\"true\"");
        await using var db = _fixture.CreateDbContext();
        (await db.ZaznamEditZamky.AnyAsync(x => x.ZaznamId == s.RecordId)).Should().BeFalse();
    }

    [Fact]
    public async Task Editor_InViewAs_ForeignLock_ShowsLockedPage_AndLockUntouched()
    {
        var s = await SeedAsync("ApiVaRo6");
        var holder = await _fixture.EnsurePersonAsync("ApiVaRo6Holder");
        var since = DateTime.UtcNow.AddMinutes(-2);
        await using (var db = _fixture.CreateDbContext())
        {
            db.ZaznamEditZamky.Add(new ZaznamEditZamekEntity { ZaznamId = s.RecordId, OsobaId = holder, ZiskanoAt = since, HeartbeatAt = since });
            await db.SaveChangesAsync();
        }
        using var client = Client();

        var html = await (await client.SendAsync(Request(HttpMethod.Get, $"/Zaznamy/Edit?id={s.RecordId}&asUser={_fixture.AdminOsobaId}", s.Cookie)))
            .Content.ReadAsStringAsync();

        html.Should().Contain("data-record-edit-locked=\"true\"", "admin vidí totéž co cílový uživatel");
        await using var check = _fixture.CreateDbContext();
        var zamek = await check.ZaznamEditZamky.AsNoTracking().SingleAsync(x => x.ZaznamId == s.RecordId);
        zamek.OsobaId.Should().Be(holder);
        zamek.HeartbeatAt.Should().BeCloseTo(since, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task KeepAlive_InViewAs_DoesNotExtendTargetLock()
    {
        var s = await SeedAsync("ApiVaRo7");
        var since = DateTime.UtcNow.AddMinutes(-3);
        await using (var db = _fixture.CreateDbContext())
        {
            db.ZaznamEditZamky.Add(new ZaznamEditZamekEntity { ZaznamId = s.RecordId, OsobaId = s.TargetId, ZiskanoAt = since, HeartbeatAt = since });
            await db.SaveChangesAsync();
        }
        using var client = Client();

        var response = await client.SendAsync(Request(HttpMethod.Get, $"/App/KeepAlive?zaznamId={s.RecordId}&asUser={_fixture.AdminOsobaId}", s.Cookie, ajax: true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var check = _fixture.CreateDbContext();
        (await check.ZaznamEditZamky.AsNoTracking().SingleAsync(x => x.ZaznamId == s.RecordId))
            .HeartbeatAt.Should().BeCloseTo(since, TimeSpan.FromSeconds(1));
    }
}
