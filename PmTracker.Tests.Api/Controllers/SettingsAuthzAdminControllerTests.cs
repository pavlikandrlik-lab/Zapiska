using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Api.Controllers;

// NOTE: After the seed-only RBAC refactor (dokončeno 2026-04-22, viz
// project_authz_architecture.md + commits 96cd687 / f1bce3f / 727a888),
// NastaveniController již nenabízí mutační endpointy SaveRole / SaveRolePermission /
// DeleteRolePermission / TogglePermission / ToggleRole / PermissionModal / RoleModal /
// RolePermissionModal. Role/akce/mapování se udržují výhradně v seedu
// (PermissionSeedConfiguration.cs) a mění se přes git/PR, ne přes UI.
//
// Tento test file nyní pokrývá pouze trvající kontrakty:
//   - /Nastaveni/Panel (read-only render sekcí + policy enforcement)
//   - /Nastaveni/UserRolesModal (čtení editoru globálních rolí uživatele)
//   - /Nastaveni/SaveUserRole + /Nastaveni/SaveUserRolesForUser (přiřazení globálních rolí)
//
// Obsolete tests pro odstraněné endpointy byly smazány — viz git log pro zdůvodnění.
[Collection(ApiSqlCollection.CollectionName)]
public sealed class SettingsAuthzAdminControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public SettingsAuthzAdminControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Panel_ShouldReturnForbidden_WhenUserLacksSettingsViewPermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSettingsPanelNoView");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/Panel?section=role-akce&asUser={userId}");

        // Class-level [Authorize(Policy = "permission:settings.view")] — authenticated user
        // bez oprávnění dostane 403 (ne 401, protože test runtime nás autentizuje přes ?asUser=).
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Panel_ShouldRenderRolePermissionSection_ForAdmin()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/Panel?section=role-akce&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        // Sekce je teď read-only — render tabulky mapování role → akce stačí; nadpis sekce panel
        // nerenderuje a mutující tlačítko („Přidat mapování") bylo odstraněno (727a888 / 96cd687).
        // Distinktivní sloupce sekce: Akce / Rozsah / Projekty (INCLUDE) / Povoleno.
        html.Should().Contain("<table").And.Contain("Projekty (INCLUDE)").And.Contain("<th>Rozsah</th>");
    }

    [Fact]
    public async Task UserRolesModal_ShouldBlockAccess_WhenUserLacksSettingsManagePermission()
    {
        var targetUserId = await _fixture.EnsurePersonAsync("ApiSettingsUserRolesTarget");
        var userId = await _fixture.EnsurePersonAsync("ApiSettingsUserRolesNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/UserRolesModal?osobaId={targetUserId}&asUser={userId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UserRolesModal_ShouldRenderUserRoleEditor_ForAdmin()
    {
        var targetUserId = await _fixture.EnsurePersonAsync("ApiSettingsUserRolesEdit");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/UserRolesModal?osobaId={targetUserId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("Upravit role uživatele");
        html.Should().Contain("action=\"/Nastaveni/SaveUserRolesForUser\"");
        html.Should().Contain($"name=\"OsobaId\" value=\"{targetUserId}\"");
    }

    [Fact]
    public async Task UserRolesModal_ShouldReturnNotFound_WhenRequestedUserDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/UserRolesModal?osobaId=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SaveUserRole_ShouldCreateAssignmentAndReturnAjaxSuccess()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSaveUserRoleUser");
        var roleId = await CreateCustomRoleAsync("ApiSaveUserRoleRole");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveUserRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("OsobaId", userId.ToString()),
                ("RoleId", roleId.ToString()),
                ("IsActive", "true")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshUrl.Should().Contain("section=uzivatele-role");

        await using var dbContext = _fixture.CreateDbContext();
        var row = await dbContext.AuthzUserRoles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OsobaId == userId && x.RoleId == roleId);

        row.Should().NotBeNull();
        row!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SaveUserRole_ShouldReturnForbidden_WhenUserLacksSettingsManagePermission()
    {
        var targetUserId = await _fixture.EnsurePersonAsync("ApiSaveUserRoleNoManageTarget");
        var roleId = await CreateCustomRoleAsync("ApiSaveUserRoleNoManageRole");
        var userId = await _fixture.EnsurePersonAsync("ApiSaveUserRoleNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveUserRole?asUser={userId}",
            ApiTestHttpHelper.BuildForm(
                ("OsobaId", targetUserId.ToString()),
                ("RoleId", roleId.ToString()),
                ("IsActive", "true")));

        var response = await client.SendAsync(request);

        // Framework-level [Authorize(Policy = "permission:settings.manage")] vrací plain 403
        // bez JSON payloadu — dřívější in-controller `hasPermission` kontrola (která vracela
        // AjaxForbiddenResult JSON) byla odstraněna ve prospěch policy attribute.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SaveUserRole_ShouldReturnValidationPayload_WhenModelStateIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveUserRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("OsobaId", string.Empty),
                ("RoleId", string.Empty),
                ("IsActive", "true")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.RequestValidationFailed);
        payload.Message.Should().Be("Přiřazení role nelze uložit.");
        payload.FieldErrors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SaveUserRolesForUser_ShouldReplaceAssignmentsAndReturnAjaxSuccess()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSaveUserRolesForUser");
        var roleIdA = await CreateCustomRoleAsync("ApiSaveUserRolesRoleA");
        var roleIdB = await CreateCustomRoleAsync("ApiSaveUserRolesRoleB");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            dbContext.AuthzUserRoles.Add(new AuthzUserRoleEntity
            {
                OsobaId = userId,
                RoleId = roleIdA,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveUserRolesForUser?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("OsobaId", userId.ToString()),
                ("RoleIds", roleIdB.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshUrl.Should().Contain("section=uzivatele-role");

        await using var verifyContext = _fixture.CreateDbContext();
        var activeRoleIds = await verifyContext.AuthzUserRoles
            .AsNoTracking()
            .Where(x => x.OsobaId == userId && x.IsActive)
            .Select(x => x.RoleId)
            .OrderBy(x => x)
            .ToListAsync();

        activeRoleIds.Should().Equal(roleIdB);
    }

    [Fact]
    public async Task SaveUserRolesForUser_ShouldReturnForbidden_WhenUserLacksSettingsManagePermission()
    {
        var targetUserId = await _fixture.EnsurePersonAsync("ApiSaveUserRolesNoManageTarget");
        var userId = await _fixture.EnsurePersonAsync("ApiSaveUserRolesNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveUserRolesForUser?asUser={userId}",
            ApiTestHttpHelper.BuildForm(("OsobaId", targetUserId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SaveUserRolesForUser_ShouldReturnValidationPayload_WhenModelStateIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveUserRolesForUser?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(("OsobaId", string.Empty)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.RequestValidationFailed);
        payload.Message.Should().Be("Role uživatele nelze uložit.");
        payload.FieldErrors.Should().NotBeEmpty();
    }

    private async Task<int> CreateCustomRoleAsync(string marker)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var normalizedMarker = string.Concat(marker.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        var codePrefix = string.IsNullOrEmpty(normalizedMarker)
            ? "APIROLE"
            : normalizedMarker[..Math.Min(8, normalizedMarker.Length)];
        var code = $"{codePrefix}{Guid.NewGuid():N}"[..20];

        var role = new AuthzRoleEntity
        {
            Kod = code,
            Nazev = marker,
            Popis = marker,
            IsSystem = false,
            IsActive = true
        };

        dbContext.AuthzRoles.Add(role);
        await dbContext.SaveChangesAsync();
        return role.Id;
    }
}
