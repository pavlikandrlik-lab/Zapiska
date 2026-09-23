using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

/// <summary>
/// Spec 2026-09-17 §4.3 — brána v GET /Zaznamy/Edit. Asertace kotví na atributy,
/// ne na český text: Razor kóduje diakritiku na HTML entity.
/// </summary>
[Collection(ApiSqlCollection.CollectionName)]
public sealed class RecordEditLockControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public RecordEditLockControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Edit_ShouldRenderLockedPage_WhenAnotherUserHoldsLock()
    {
        var holderId = await _fixture.EnsurePersonAsync("ApiLockHolder", "Jan", "Novák");
        var projectId = await _fixture.EnsureProjectAsync("APILOCK1");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APILOCK1SUB", holderId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, holderId, subsystemId, "U", "API lock record");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            dbContext.ZaznamEditZamky.Add(new ZaznamEditZamekEntity
            {
                ZaznamId = recordId,
                OsobaId = holderId,
                ZiskanoAt = DateTime.UtcNow,
                HeartbeatAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-record-edit-locked=\"true\"",
            "druhý uživatel musí dostat stránku o zamčeném záznamu");
        html.Should().NotContain("data-record-editor-form=\"true\"",
            "editor se nesmí vůbec vyrenderovat");
        html.Should().Contain("Nov&#xE1;k", "hláška musí pojmenovat držitele zámku (Příjmení Jméno)");
    }

    [Fact]
    public async Task Edit_ShouldOpenEditor_AndTakeLock_WhenRecordIsFree()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiLockFreeOwner");
        var projectId = await _fixture.EnsureProjectAsync("APILOCK2");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APILOCK2SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API free record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-record-editor-form=\"true\"");
        html.Should().NotContain("data-record-edit-locked=\"true\"");

        await using var dbContext = _fixture.CreateDbContext();
        var zamek = await dbContext.ZaznamEditZamky.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ZaznamId == recordId);
        zamek.Should().NotBeNull("otevření editoru zabírá zámek");
        zamek!.OsobaId.Should().Be(_fixture.AdminOsobaId);
    }

    [Fact]
    public async Task KeepAlive_ShouldRefreshHeartbeat_OfOwnLock()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiLockHeartbeatOwner");
        var projectId = await _fixture.EnsureProjectAsync("APILOCK4");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APILOCK4SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API heartbeat record");

        var stale = DateTime.UtcNow.AddMinutes(-10);
        await using (var dbContext = _fixture.CreateDbContext())
        {
            dbContext.ZaznamEditZamky.Add(new ZaznamEditZamekEntity
            {
                ZaznamId = recordId,
                OsobaId = _fixture.AdminOsobaId,
                ZiskanoAt = stale,
                HeartbeatAt = stale
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var response = await client.GetAsync($"/App/KeepAlive?zaznamId={recordId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verifyContext = _fixture.CreateDbContext();
        var zamek = await verifyContext.ZaznamEditZamky.AsNoTracking().SingleAsync(x => x.ZaznamId == recordId);
        zamek.HeartbeatAt.Should().BeAfter(stale, "keep-alive nese heartbeat zámku");
        zamek.ZiskanoAt.Should().BeCloseTo(stale, TimeSpan.FromSeconds(1),
            "čas začátku úprav heartbeat neposouvá");
    }

    [Fact]
    public async Task ReleaseEditLock_ShouldRemoveOwnLock()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiLockReleaseOwner");
        var projectId = await _fixture.EnsureProjectAsync("APILOCK5");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APILOCK5SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API release record");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            dbContext.ZaznamEditZamky.Add(new ZaznamEditZamekEntity
            {
                ZaznamId = recordId,
                OsobaId = _fixture.AdminOsobaId,
                ZiskanoAt = DateTime.UtcNow,
                HeartbeatAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsync(
            $"/Zaznamy/ReleaseEditLock?asUser={_fixture.AdminOsobaId}",
            new StringContent($"{{\"zaznamId\":{recordId}}}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verifyContext = _fixture.CreateDbContext();
        (await verifyContext.ZaznamEditZamky.AsNoTracking().AnyAsync(x => x.ZaznamId == recordId))
            .Should().BeFalse("beacon z pagehide zámek uvolní");
    }

    [Fact]
    public async Task Edit_ShouldReopenForSameUser_WhenLockIsAlreadyHeldByThem()
    {
        var ownerId = await _fixture.EnsurePersonAsync("ApiLockReentrantOwner");
        var projectId = await _fixture.EnsureProjectAsync("APILOCK3");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APILOCK3SUB", ownerId);
        var recordId = await _fixture.EnsureRecordAsync(projectId, ownerId, subsystemId, "U", "API reentrant record");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var response = await client.GetAsync($"/Zaznamy/Edit?id={recordId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("data-record-editor-form=\"true\"",
            "druhý tab téhož uživatele nesmí zamknout sám sebe");
    }
}
