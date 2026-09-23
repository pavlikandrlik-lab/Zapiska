using System.Net;
using FluentAssertions;
using PmTracker.Tests.Api.TestInfrastructure;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class BreadcrumbRenderTests
{
    private readonly ApiSqlFixture _fixture;

    public BreadcrumbRenderTests(ApiSqlFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ProjectDetail_RendersBreadcrumbBar_WithProjectCrumbAndCloseToList()
    {
        var ownerId = await _fixture.EnsurePersonAsync("BcProjOwner");
        var projectId = await _fixture.EnsureProjectAsync("BCPROJ");
        var subsystemId = await _fixture.EnsureSubsystemAsync("BCPROJSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar", html);
        html.Should().Contain("aria-current=\"page\"");                 // projekt = aktuální
        html.Should().Contain("app-breadcrumb-suffix");                 // " | ZKR"
        html.Should().Contain($"/Projekty?asUser={_fixture.AdminOsobaId}"); // ✕/← cíl = seznam
        // starý page-header už NE:
        html.Should().NotContain("project-title-inline");
    }

    [Fact]
    public async Task MeetingDetail_RendersProjectThenMeetingCrumbs()
    {
        var ownerId = await _fixture.EnsurePersonAsync("BcMeetOwner");
        var projectId = await _fixture.EnsureProjectAsync("BCMEET");
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var meetingId = await _fixture.CreateMeetingAsync(projectId, "OPEN", 9101);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/Detail/{meetingId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}"); // projekt crumb + ✕ jednání cíl
        html.Should().Contain("app-breadcrumb-close");
    }

    [Fact]
    public async Task RecordEditor_RendersRecordAsCurrentCrumb_UnderProject()
    {
        var ownerId = await _fixture.EnsurePersonAsync("BcRecOwner");
        var projectId = await _fixture.EnsureProjectAsync("BCREC");
        var subsystemId = await _fixture.EnsureSubsystemAsync("BCRECSUB", ownerId);
        await _fixture.EnsureProjectTeamMemberAsync(projectId, ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "Bc editor record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit/{recordId}?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("app-breadcrumb-bar");
        html.Should().Contain("app-breadcrumb-current");
        // 2026-09-03: drobeček nese PROJEKTOVÉ číslo záznamu (např. #901-1), ne databázové Id —
        // sjednoceno se stránkou záznamu, kde se stejný záznam tvářil jinak.
        // Razor HTML-enkóduje diakritiku (á → &#xE1;), proto ověřujeme ASCII část textu „Zá[znam #…]".
        // (Že jde o projektové číslo a ne databázové Id, hlídá
        // RecordDetailPageRenderTests.EditorBreadcrumb_UsesProjectVisibleNumber_LikeDetailPage —
        // v této fixture bývá Id i projektové číslo shodně 1, takže by se tu nedaly rozlišit.)
        var visibleNumber = await _fixture.GetRecordVisibleNumberAsync(recordId);
        html.Should().Contain($"znam #{visibleNumber}");
        html.Should().Contain($"/Projekty/Detail/{projectId}?asUser={_fixture.AdminOsobaId}"); // ✕ záznamu → projekt
    }
}
