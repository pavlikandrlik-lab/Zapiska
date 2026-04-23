using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class SettingsAuthzAdminControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public SettingsAuthzAdminControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Panel_ShouldReturnUnauthorized_WhenUserLacksSettingsViewPermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSettingsPanelNoView");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/Panel?section=role-akce&asUser={userId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Panel_ShouldRenderRolePermissionSection_ForAdmin()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/Panel?section=role-akce&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("Mapování role -> akce");
        html.Should().Contain("Přidat mapování");
    }

    [Fact]
    public async Task RoleModal_ShouldBlockAccess_WhenUserLacksSettingsManagePermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSettingsRoleModalNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RoleModal?asUser={userId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task RoleModal_ShouldRenderCreateModal_ForAdmin()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RoleModal?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("authz-role-modal-title");
        html.Should().Contain("action=\"/Nastaveni/SaveRole\"");
    }

    [Fact]
    public async Task RoleModal_ShouldReturnNotFound_WhenRequestedRoleDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RoleModal?id=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RoleModal_ShouldBlockSystemRoleEdit_WhenRoleIsSystem()
    {
        var systemRoleId = await GetOrCreateSystemRoleIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RoleModal?id={systemRoleId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PermissionModal_ShouldBlockAccess_WhenUserLacksSettingsManagePermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSettingsPermModalNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/PermissionModal?asUser={userId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PermissionModal_ShouldRenderCreateModal_ForAdmin()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/PermissionModal?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("authz-permission-modal-title");
        html.Should().Contain("action=\"/Nastaveni/SavePermission\"");
    }

    [Fact]
    public async Task PermissionModal_ShouldReturnNotFound_WhenRequestedPermissionDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/PermissionModal?id=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PermissionModal_ShouldBlockSystemPermissionEdit_WhenPermissionIsSystem()
    {
        var systemPermissionId = await GetOrCreateSystemPermissionIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/PermissionModal?id={systemPermissionId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
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
    public async Task RolePermissionModal_ShouldBlockAccess_WhenUserLacksSettingsManagePermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSettingsRolePermNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RolePermissionModal?asUser={userId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task RolePermissionModal_ShouldRenderCreateModal_ForAdmin()
    {
        var roleId = await CreateCustomRoleAsync("ApiRolePermModalRole");
        var permissionId = await CreateCustomPermissionAsync("ApiRolePermModalPerm");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RolePermissionModal?roleId={roleId}&permissionId={permissionId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("authz-role-permission-modal-title");
        html.Should().Contain("action=\"/Nastaveni/SaveRolePermission\"");
        html.Should().Contain("name=\"RoleId\"");
        html.Should().Contain("name=\"PermissionId\"");
    }

    [Fact]
    public async Task RolePermissionModal_ShouldReturnNotFound_WhenRequestedMappingDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RolePermissionModal?id=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RolePermissionModal_ShouldBlockDeniedMapping_WhenMappingIsNotAllowed()
    {
        var roleId = await CreateCustomRoleAsync("ApiRolePermDeniedRole");
        var permissionId = await CreateCustomPermissionAsync("ApiRolePermDeniedPermission");
        var mappingId = await CreateRolePermissionMappingAsync(roleId, permissionId, ScopeMode.All, [], isAllowed: false);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Nastaveni/RolePermissionModal?id={mappingId}&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SaveRole_ShouldCreateRoleAndReturnAjaxSuccess()
    {
        var marker = $"APISR{Guid.NewGuid():N}";
        var roleCode = marker[..16];

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Kod", roleCode),
                ("Nazev", "API SaveRole"),
                ("Popis", "API role test")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("nastaveni-panel");
        payload.RefreshUrl.Should().Contain("section=role");

        await using var dbContext = _fixture.CreateDbContext();
        var saved = await dbContext.AuthzRoles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Kod == roleCode);

        saved.Should().NotBeNull();
        saved!.IsSystem.Should().BeFalse();
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SaveRole_ShouldReturnForbiddenPayload_WhenUserLacksSettingsManagePermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiSaveRoleNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRole?asUser={userId}",
            ApiTestHttpHelper.BuildForm(
                ("Kod", "API_NO_MANAGE"),
                ("Nazev", "No manage")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        AssertForbiddenPayload(payload);
    }

    [Fact]
    public async Task SaveRole_ShouldReturnValidationPayload_WhenModelStateIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Kod", string.Empty),
                ("Nazev", string.Empty)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.RequestValidationFailed);
        payload.Message.Should().Be("Roli nelze uložit.");
        payload.FieldErrors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SaveRole_ShouldRedirectToSettingsSection_WhenRequestIsNotAjaxAndSucceeds()
    {
        var roleCode = $"APINONAJ{Guid.NewGuid():N}"[..16];

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(BuildPostRequest(
            $"/Nastaveni/SaveRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Kod", roleCode),
                ("Nazev", "NonAjax role"),
                ("Popis", "NonAjax role"))));

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/Nastaveni");
        response.Headers.Location!.ToString().Should().Contain("section=role");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.AuthzRoles.AsNoTracking().AnyAsync(x => x.Kod == roleCode)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveRole_ShouldRedirectWithTempDataError_WhenRequestIsNotAjaxAndModelIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var postResponse = await client.SendAsync(BuildPostRequest(
            $"/Nastaveni/SaveRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Kod", string.Empty),
                ("Nazev", string.Empty))));

        postResponse.StatusCode.Should().Be(HttpStatusCode.Found);
        postResponse.Headers.Location.Should().NotBeNull();
        var followUrl = AppendAsUser(postResponse.Headers.Location!.ToString(), _fixture.AdminOsobaId);

        var followResponse = await client.GetAsync(followUrl);
        var html = await followResponse.Content.ReadAsStringAsync();
        followResponse.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("alert-error");
        html.Should().Contain("Roli nelze ulo");
    }

    [Fact]
    public async Task ToggleRole_ShouldUpdateRoleStateAndReturnAjaxSuccess()
    {
        var roleId = await CreateCustomRoleAsync("ApiToggleRole");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/ToggleRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", roleId.ToString()),
                ("IsActive", "false")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshUrl.Should().Contain("section=role");

        await using var dbContext = _fixture.CreateDbContext();
        var updatedRole = await dbContext.AuthzRoles.AsNoTracking().SingleAsync(x => x.Id == roleId);
        updatedRole.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleRole_ShouldReturnForbiddenPayload_WhenUserLacksSettingsManagePermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiToggleRoleNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/ToggleRole?asUser={userId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", "1"),
                ("IsActive", "false")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        AssertForbiddenPayload(payload);
    }

    [Fact]
    public async Task ToggleRole_ShouldReturnAjaxError_WhenRoleDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/ToggleRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", "999999"),
                ("IsActive", "false")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Role nebyla nalezena");
    }

    [Fact]
    public async Task TogglePermission_ShouldUpdatePermissionStateAndReturnAjaxSuccess()
    {
        var permissionId = await CreateCustomPermissionAsync("ApiTogglePermission");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/TogglePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", permissionId.ToString()),
                ("IsActive", "false")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshUrl.Should().Contain("section=akce");

        await using var dbContext = _fixture.CreateDbContext();
        var updatedPermission = await dbContext.AuthzPermissions.AsNoTracking().SingleAsync(x => x.Id == permissionId);
        updatedPermission.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task TogglePermission_ShouldReturnForbiddenPayload_WhenUserLacksSettingsManagePermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiTogglePermissionNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/TogglePermission?asUser={userId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", "1"),
                ("IsActive", "false")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        AssertForbiddenPayload(payload);
    }

    [Fact]
    public async Task TogglePermission_ShouldReturnAjaxError_WhenPermissionDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/TogglePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", "999999"),
                ("IsActive", "false")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Akce nebyla nalezena");
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
    public async Task SaveUserRole_ShouldReturnForbiddenPayload_WhenUserLacksSettingsManagePermission()
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

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        AssertForbiddenPayload(payload);
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
    public async Task SaveUserRolesForUser_ShouldReturnForbiddenPayload_WhenUserLacksSettingsManagePermission()
    {
        var targetUserId = await _fixture.EnsurePersonAsync("ApiSaveUserRolesNoManageTarget");
        var userId = await _fixture.EnsurePersonAsync("ApiSaveUserRolesNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveUserRolesForUser?asUser={userId}",
            ApiTestHttpHelper.BuildForm(("OsobaId", targetUserId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        AssertForbiddenPayload(payload);
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

    [Fact]
    public async Task SaveRolePermission_ShouldCreateIncludeMappingAndPersistProjectLinks()
    {
        var roleId = await CreateCustomRoleAsync("ApiSaveRolePermRole");
        var permissionId = await CreateCustomPermissionAsync("ApiSaveRolePermPerm");
        var projectId = await _fixture.EnsureProjectAsync("APISRPINCL");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("RoleId", roleId.ToString()),
                ("PermissionId", permissionId.ToString()),
                ("ScopeMode", "INCLUDE"),
                ("IsAllowed", "false"),
                ("ProjektIds", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshUrl.Should().Contain("section=role-akce");

        await using var dbContext = _fixture.CreateDbContext();
        var row = await dbContext.AuthzRolePermissions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId);

        row.Should().NotBeNull();
        row!.ScopeMode.Should().Be(ScopeMode.Include);
        row.IsAllowed.Should().BeTrue();

        var links = await dbContext.AuthzRolePermissionProjects
            .AsNoTracking()
            .Where(x => x.RolePermissionId == row.Id)
            .Select(x => x.ProjektId)
            .ToListAsync();

        links.Should().Equal(projectId);
    }

    [Fact]
    public async Task SaveRolePermission_ShouldReturnForbiddenPayload_WhenUserLacksSettingsManagePermission()
    {
        var roleId = await CreateCustomRoleAsync("ApiSaveRolePermNoManageRole");
        var permissionId = await CreateCustomPermissionAsync("ApiSaveRolePermNoManagePerm");
        var userId = await _fixture.EnsurePersonAsync("ApiSaveRolePermNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRolePermission?asUser={userId}",
            ApiTestHttpHelper.BuildForm(
                ("RoleId", roleId.ToString()),
                ("PermissionId", permissionId.ToString()),
                ("ScopeMode", "ALL")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        AssertForbiddenPayload(payload);
    }

    [Fact]
    public async Task SaveRolePermission_ShouldReturnValidationPayload_WhenModelStateIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("RoleId", string.Empty),
                ("PermissionId", string.Empty),
                ("ScopeMode", "ALL")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.RequestValidationFailed);
        payload.Message.Should().Be("Mapování role/akce nelze uložit.");
        payload.FieldErrors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SaveRolePermission_ShouldReturnAjaxError_WhenDuplicateMappingExists()
    {
        var roleId = await CreateCustomRoleAsync("ApiSaveRolePermDuplicateRole");
        var permissionIdA = await CreateCustomPermissionAsync("ApiSaveRolePermDuplicatePermissionA");
        var permissionIdB = await CreateCustomPermissionAsync("ApiSaveRolePermDuplicatePermissionB");
        var mappingIdA = await CreateRolePermissionMappingAsync(roleId, permissionIdA, ScopeMode.All, []);
        _ = await CreateRolePermissionMappingAsync(roleId, permissionIdB, ScopeMode.All, []);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", mappingIdA.ToString()),
                ("RoleId", roleId.ToString()),
                ("PermissionId", permissionIdB.ToString()),
                ("ScopeMode", "ALL"))));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("už mapování existuje");
    }

    [Fact]
    public async Task SaveRolePermission_ShouldReturnAjaxError_WhenScopeModeIsInvalid()
    {
        var roleId = await CreateCustomRoleAsync("ApiSaveRolePermScopeRole");
        var permissionId = await CreateCustomPermissionAsync("ApiSaveRolePermScopePermission");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.SendAsync(ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("RoleId", roleId.ToString()),
                ("PermissionId", permissionId.ToString()),
                ("ScopeMode", "INVALID"))));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Neplatný rozsah mapování role/akce");
    }

    [Fact]
    public async Task SaveRolePermission_ShouldReturnAjaxError_WhenIncludeProjectsContainUnknownId()
    {
        var roleId = await CreateCustomRoleAsync("ApiSaveRolePermInvalidProjectRole");
        var permissionId = await CreateCustomPermissionAsync("ApiSaveRolePermInvalidProjectPerm");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/SaveRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("RoleId", roleId.ToString()),
                ("PermissionId", permissionId.ToString()),
                ("ScopeMode", "INCLUDE"),
                ("ProjektIds", "999999")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Vybrané projekty pro INCLUDE mapování neexistují");
    }

    [Fact]
    public async Task SaveRolePermission_ShouldRedirectWithTempDataError_WhenRequestIsNotAjaxAndMappingIsDuplicate()
    {
        var roleId = await CreateCustomRoleAsync("ApiSaveRolePermRedirectDupRole");
        var permissionIdA = await CreateCustomPermissionAsync("ApiSaveRolePermRedirectDupPermissionA");
        var permissionIdB = await CreateCustomPermissionAsync("ApiSaveRolePermRedirectDupPermissionB");
        var mappingIdA = await CreateRolePermissionMappingAsync(roleId, permissionIdA, ScopeMode.All, []);
        _ = await CreateRolePermissionMappingAsync(roleId, permissionIdB, ScopeMode.All, []);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var postResponse = await client.SendAsync(BuildPostRequest(
            $"/Nastaveni/SaveRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", mappingIdA.ToString()),
                ("RoleId", roleId.ToString()),
                ("PermissionId", permissionIdB.ToString()),
                ("ScopeMode", "ALL"))));

        postResponse.StatusCode.Should().Be(HttpStatusCode.Found);
        postResponse.Headers.Location.Should().NotBeNull();
        var followUrl = AppendAsUser(postResponse.Headers.Location!.ToString(), _fixture.AdminOsobaId);

        var followResponse = await client.GetAsync(followUrl);
        var html = await followResponse.Content.ReadAsStringAsync();
        followResponse.StatusCode.Should().Be(HttpStatusCode.OK, html);
        html.Should().Contain("alert alert-error");
        html.Should().Contain("mapov");
        html.Should().Contain("existuje");
    }

    [Fact]
    public async Task DeleteRolePermission_ShouldDeleteMappingAndIncludeProjectLinks()
    {
        var roleId = await CreateCustomRoleAsync("ApiDeleteRolePermRole");
        var permissionId = await CreateCustomPermissionAsync("ApiDeleteRolePermPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIDELRP");
        var mappingId = await CreateRolePermissionMappingAsync(roleId, permissionId, ScopeMode.Include, [projectId]);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/DeleteRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(("Id", mappingId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshUrl.Should().Contain("section=role-akce");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.AuthzRolePermissions.AnyAsync(x => x.Id == mappingId)).Should().BeFalse();
        (await dbContext.AuthzRolePermissionProjects.AnyAsync(x => x.RolePermissionId == mappingId)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteRolePermission_ShouldReturnForbiddenPayload_WhenUserLacksSettingsManagePermission()
    {
        var userId = await _fixture.EnsurePersonAsync("ApiDeleteRolePermNoManage");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/DeleteRolePermission?asUser={userId}",
            ApiTestHttpHelper.BuildForm(("Id", "1")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        AssertForbiddenPayload(payload);
    }

    [Fact]
    public async Task DeleteRolePermission_ShouldReturnValidationPayload_WhenIdIsInvalid()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Nastaveni/DeleteRolePermission?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(("Id", "0")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.RequestValidationFailed);
        payload.Message.Should().Be("Mapování role/akce nelze smazat.");
        payload.FieldErrors.Should().ContainKey("Id");
    }

    private static void AssertForbiddenPayload(PmTracker.Web.Models.ViewModels.ModalSubmitResultViewModel payload)
    {
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Nemáte oprávnění");
        payload.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    private static HttpRequestMessage BuildPostRequest(string url, HttpContent content)
    {
        return new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
    }

    private static string AppendAsUser(string location, int userId)
    {
        return location.Contains('?', StringComparison.Ordinal)
            ? $"{location}&asUser={userId}"
            : $"{location}?asUser={userId}";
    }

    private async Task<int> GetOrCreateSystemRoleIdAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();

        var existingId = await dbContext.AuthzRoles
            .AsNoTracking()
            .Where(x => x.IsSystem)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var role = new AuthzRoleEntity
        {
            Kod = $"SYSMOD{Guid.NewGuid():N}"[..20],
            Nazev = "System Modal Role",
            Popis = "System role for modal guard tests",
            IsSystem = true,
            IsActive = true
        };
        dbContext.AuthzRoles.Add(role);
        await dbContext.SaveChangesAsync();
        return role.Id;
    }

    private async Task<int> GetOrCreateSystemPermissionIdAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();

        var existingId = await dbContext.AuthzPermissions
            .AsNoTracking()
            .Where(x => x.IsSystem)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();
        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var categoryId = await dbContext.AuthzPermissionCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var permission = new AuthzPermissionEntity
        {
            Klic = $"settings.system.modal.{Guid.NewGuid():N}"[..60],
            Nazev = "System Modal Permission",
            CategoryId = categoryId,
            ScopeLevel = PermissionScopeLevel.Global,
            IsActive = true,
            IsSystem = true
        };
        dbContext.AuthzPermissions.Add(permission);
        await dbContext.SaveChangesAsync();
        return permission.Id;
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

    private async Task<int> CreateCustomPermissionAsync(string marker)
    {
        await using var dbContext = _fixture.CreateDbContext();
        var categoryId = await dbContext.AuthzPermissionCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();

        var permission = new AuthzPermissionEntity
        {
            Klic = $"settings.api.{marker.ToLowerInvariant()}.{Guid.NewGuid():N}"[..60],
            Nazev = marker,
            CategoryId = categoryId,
            ScopeLevel = PermissionScopeLevel.Project,
            IsActive = true,
            IsSystem = false
        };

        dbContext.AuthzPermissions.Add(permission);
        await dbContext.SaveChangesAsync();
        return permission.Id;
    }

    private async Task<int> CreateRolePermissionMappingAsync(int roleId, int permissionId, ScopeMode scopeMode, IReadOnlyCollection<int> projectIds, bool isAllowed = true)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var mapping = new AuthzRolePermissionEntity
        {
            RoleId = roleId,
            PermissionId = permissionId,
            ScopeMode = scopeMode,
            IsAllowed = isAllowed
        };

        dbContext.AuthzRolePermissions.Add(mapping);
        await dbContext.SaveChangesAsync();

        foreach (var projectId in projectIds)
        {
            dbContext.AuthzRolePermissionProjects.Add(new AuthzRolePermissionProjectEntity
            {
                RolePermissionId = mapping.Id,
                ProjektId = projectId
            });
        }

        await dbContext.SaveChangesAsync();
        return mapping.Id;
    }
}
