using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectsCommandsControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectsCommandsControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveProject_ShouldCreateProject_AndReturnAjaxSuccess()
    {
        var statusCode = await GetActiveProjectStatusCodeAsync();
        var marker = $"APICREATE_{Guid.NewGuid():N}"[..16];

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveProject?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Nazev", "  API Create Project  "),
                ("Zkratka", marker),
                ("Stav", statusCode),
                ("PouzivatIdentJednani", "true")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.RefreshScope.Should().Be("projekty-index");
        payload.ProjectId.Should().HaveValue();
        payload.Message.Should().Be("Projekt byl uložen.");

        await using var dbContext = _fixture.CreateDbContext();
        var project = await dbContext.Projekty.AsNoTracking().SingleAsync(x => x.Id == payload.ProjectId!.Value);
        project.CelyNazev.Should().Be("API Create Project", "ukládání projektu trimuje název");
        project.Zkratka.Should().Be(marker);
        project.PouzivatIdentJednani.Should().BeTrue();
    }

    [Fact]
    public async Task SaveProject_ShouldUpdateProject_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIUPDPRJ");
        var statusCode = await GetActiveProjectStatusCodeAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveProject?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", projectId.ToString()),
                ("Nazev", "Updated API project"),
                ("Zkratka", "APIUPDPRJ"),
                ("Stav", statusCode),
                ("PouzivatIdentJednani", "true")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);

        await using var dbContext = _fixture.CreateDbContext();
        var project = await dbContext.Projekty.AsNoTracking().SingleAsync(x => x.Id == projectId);
        project.CelyNazev.Should().Be("Updated API project");
        project.PouzivatIdentJednani.Should().BeTrue();
    }

    [Fact]
    public async Task SaveProject_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiSaveProjectNoPerm");
        var statusCode = await GetActiveProjectStatusCodeAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveProject?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("Nazev", "Unauthorized project"),
                ("Zkratka", "APIUNAUTHPRJ"),
                ("Stav", statusCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Nemáte oprávnění");
    }

    [Fact]
    public async Task DeleteProject_ShouldSoftDeleteProject_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync($"APIDEL_{Guid.NewGuid():N}"[..16]);
        var expectedDeletedStatusId = await GetDeletedProjectStatusIdAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeleteProject?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("PotvrditSmazani", "true")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.RefreshScope.Should().Be("projekty-index");
        payload.Message.Should().Be("Projekt byl smazán.");

        await using var dbContext = _fixture.CreateDbContext();
        var project = await dbContext.Projekty.AsNoTracking().SingleAsync(x => x.Id == projectId);
        project.StavId.Should().Be(expectedDeletedStatusId);
    }

    [Fact]
    public async Task DeleteProject_ShouldReturnValidationError_WhenDeleteNotConfirmed()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIDEL_VALID");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeleteProject?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("PotvrditSmazani", "false")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.RequestValidationFailed);
        payload.FieldErrors.Keys.Should().Contain(key => key.Contains("PotvrditSmazani", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteProject_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiDeleteProjectNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIDEL_DENY");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeleteProject?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("PotvrditSmazani", "true")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
    }

    [Fact]
    public async Task SaveTeamMember_ShouldPersistAssignment_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync("APITEAM_SAVE");
        var memberId = await _fixture.EnsurePersonAsync("ApiTeamMemberToSave");
        var roleCode = await GetAnyProjectRoleCodeAsync();
        var expectedRoleId = await GetProjectRoleIdByCodeAsync(roleCode);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveTeamMember?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("OsobaId", memberId.ToString()),
                ("Role", roleCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.RefreshScope.Should().Be("projekty-detail-tym");
        payload.Tab.Should().Be("tym");
        payload.Message.Should().Be("Člen týmu byl uložen.");

        await using var dbContext = _fixture.CreateDbContext();
        var assignment = await dbContext.ObsazeniProjektu.AsNoTracking()
            .SingleAsync(x => x.ProjektId == projectId && x.OsobaId == memberId);
        assignment.RoleId.Should().Be(expectedRoleId);
    }

    [Fact]
    public async Task SaveTeamMember_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiSaveTeamNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APITEAM_SAVE_DENY");
        var memberId = await _fixture.EnsurePersonAsync("ApiSaveTeamDeniedMember");
        var roleCode = await GetAnyProjectRoleCodeAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveTeamMember?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("OsobaId", memberId.ToString()),
                ("Role", roleCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
    }

    [Fact]
    public async Task RemoveTeamMember_ShouldDeleteAssignment_AndRedirectToTeamTab()
    {
        var projectId = await _fixture.EnsureProjectAsync("APITEAM_REMOVE");
        var memberId = await _fixture.EnsurePersonAsync("ApiTeamMemberToRemove");
        var roleCode = await GetAnyProjectRoleCodeAsync();
        await EnsureProjectRoleAssignmentAsync(projectId, memberId, roleCode);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsync(
            $"/Projekty/RemoveTeamMember?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("OsobaId", memberId.ToString())));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/Projekty/Detail");
        response.Headers.Location!.ToString().Should().Contain("tab=tym");

        await using var dbContext = _fixture.CreateDbContext();
        (await dbContext.ObsazeniProjektu.AsNoTracking()
            .AnyAsync(x => x.ProjektId == projectId && x.OsobaId == memberId)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveTeamMember_ShouldReturnForbidden_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiRemoveTeamNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APITEAM_REMOVE_DENY");
        var memberId = await _fixture.EnsurePersonAsync("ApiRemoveTeamDeniedMember");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.PostAsync(
            $"/Projekty/RemoveTeamMember?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("OsobaId", memberId.ToString())));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateProjectRole_ShouldSetDatumOdebrani_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIPROJROLE_DEACT");
        var memberId = await _fixture.EnsurePersonAsync("ApiProjectRoleDeactivate");
        var roleCode = await GetAnyProjectRoleCodeAsync();
        var projectRoleId = await EnsureProjectRoleAssignmentAsync(projectId, memberId, roleCode);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeactivateProjectRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektRoleId", projectRoleId.ToString()),
                ("projektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.RefreshScope.Should().Be("projekty-detail-tym");
        payload.Tab.Should().Be("tym");
        payload.Message.Should().Be("Projektová role byla deaktivována.");

        await using var dbContext = _fixture.CreateDbContext();
        var assignment = await dbContext.ObsazeniProjektu.AsNoTracking().SingleAsync(x => x.Id == projectRoleId);
        assignment.DatumOdebrani.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivateProjectRole_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiDeactivateRoleNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIPROJROLE_DEACT_DENY");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeactivateProjectRole?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektRoleId", "1"),
                ("projektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
    }

    [Fact]
    public async Task AssignProjectSubsystem_ShouldPersistAssignment_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync("APISUB_ASSIGN");
        var subsystemCode = $"APISUB_{Guid.NewGuid():N}"[..14];
        await _fixture.EnsureSubsystemAsync(subsystemCode, _fixture.AdminOsobaId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/AssignProjectSubsystem?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("SubsystemKod", subsystemCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.RefreshScope.Should().Be("projekty-detail-tym");
        payload.Tab.Should().Be("tym");
        payload.Message.Should().Be("Subsystém byl přiřazen k projektu.");

        await using var dbContext = _fixture.CreateDbContext();
        var subsystemId = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Kod == subsystemCode)
            .Select(x => x.Id)
            .SingleAsync();
        var assignment = await dbContext.ProjektSubsystemy.AsNoTracking()
            .SingleAsync(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue);
        assignment.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AssignProjectSubsystem_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiAssignSubsystemNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APISUB_ASSIGN_DENY");
        var subsystemCode = $"APISB_DENY_{Guid.NewGuid():N}"[..14];
        await _fixture.EnsureSubsystemAsync(subsystemCode, _fixture.AdminOsobaId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/AssignProjectSubsystem?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("SubsystemKod", subsystemCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
    }

    [Fact]
    public async Task DeactivateProjectSubsystem_ShouldSetDatumOdebrani_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync("APISUB_DEACT");
        var subsystemCode = $"APISUBOFF_{Guid.NewGuid():N}"[..14];
        var subsystemId = await _fixture.EnsureSubsystemAsync(subsystemCode, _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeactivateProjectSubsystem?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektSubsystemId", projectSubsystemId.ToString()),
                ("projektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.RefreshScope.Should().Be("projekty-detail-tym");
        payload.Tab.Should().Be("tym");
        payload.Message.Should().Be("Subsystém projektu byl deaktivován.");

        await using var dbContext = _fixture.CreateDbContext();
        var assignment = await dbContext.ProjektSubsystemy.AsNoTracking().SingleAsync(x => x.Id == projectSubsystemId);
        assignment.DatumOdebrani.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivateProjectSubsystem_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiDeactivateSubsystemNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APISUB_DEACT_DENY");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeactivateProjectSubsystem?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektSubsystemId", "1"),
                ("projektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
    }

    [Fact]
    public async Task AssignProjectSubsystemRole_ShouldPersistAssignment_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync("APISUBROLE_ASSIGN");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"APISRASS_{Guid.NewGuid():N}"[..12], _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);
        var personId = await _fixture.EnsurePersonAsync("ApiSubsystemRoleAssign");
        var roleCode = await GetAnySubsystemRoleCodeAsync();
        var expectedRoleId = await GetSubsystemRoleIdByCodeAsync(roleCode);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/AssignProjectSubsystemRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ProjektSubsystemId", projectSubsystemId.ToString()),
                ("OsobaId", personId.ToString()),
                ("RoleKod", roleCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.RefreshScope.Should().Be("projekty-detail-tym");
        payload.Tab.Should().Be("tym");
        payload.Message.Should().Be("Role v subsystému byla přiřazena.");

        await using var dbContext = _fixture.CreateDbContext();
        var assignment = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking()
            .SingleAsync(x =>
                x.ProjektSubsystemId == projectSubsystemId &&
                x.OsobaId == personId &&
                x.RoleSubsystemuId == expectedRoleId &&
                !x.DatumOdebrani.HasValue);
        assignment.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AssignProjectSubsystemRole_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiAssignSubsystemRoleNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APISUBROLE_ASSIGN_DENY");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"APISRNO_{Guid.NewGuid():N}"[..12], _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);
        var personId = await _fixture.EnsurePersonAsync("ApiAssignSubsystemRoleDeniedPerson");
        var roleCode = await GetAnySubsystemRoleCodeAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/AssignProjectSubsystemRole?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ProjektSubsystemId", projectSubsystemId.ToString()),
                ("OsobaId", personId.ToString()),
                ("RoleKod", roleCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
    }

    [Fact]
    public async Task DeactivateProjectSubsystemRole_ShouldSetDatumOdebrani_AndReturnAjaxSuccess()
    {
        var projectId = await _fixture.EnsureProjectAsync("APISUBROLE_DEACT");
        var subsystemId = await _fixture.EnsureSubsystemAsync($"APISR_{Guid.NewGuid():N}"[..12], _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);
        var personId = await _fixture.EnsurePersonAsync("ApiSubsystemRoleDeactivate");
        var roleCode = await GetAnySubsystemRoleCodeAsync();
        var projectSubsystemRoleId = await EnsureProjectSubsystemRoleAssignmentAsync(projectSubsystemId, personId, roleCode);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeactivateProjectSubsystemRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektSubsystemRoleId", projectSubsystemRoleId.ToString()),
                ("projektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeTrue();
        payload.ProjectId.Should().Be(projectId);
        payload.RefreshScope.Should().Be("projekty-detail-tym");
        payload.Tab.Should().Be("tym");
        payload.Message.Should().Be("Role v subsystému byla deaktivována.");

        await using var dbContext = _fixture.CreateDbContext();
        var assignment = await dbContext.ObsazeniSubsystemuProjektu.AsNoTracking().SingleAsync(x => x.Id == projectSubsystemRoleId);
        assignment.DatumOdebrani.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivateProjectSubsystemRole_ShouldReturnForbiddenPayload_WhenUserLacksPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiDeactivateSubsystemRoleNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APISUBROLE_DEACT_DENY");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeactivateProjectSubsystemRole?asUser={outsiderId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektSubsystemRoleId", "1"),
                ("projektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
    }

    private async Task<string> GetActiveProjectStatusCodeAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var statusCodes = await dbContext.CiselnikStavuProjektu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .ToListAsync();

        return statusCodes.First(x => !string.Equals(x, "DELETED", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<int> GetDeletedProjectStatusIdAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        var statusRows = await dbContext.CiselnikStavuProjektu.AsNoTracking()
            .Select(x => new { x.Id, x.Kod, x.Nazev })
            .ToListAsync();

        return statusRows
            .Where(x =>
                string.Equals(x.Kod, "DELETED", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Nazev) && x.Nazev.Contains("smaz", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .First();
    }

    private async Task<string> GetAnyProjectRoleCodeAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
    }

    private async Task<int> GetProjectRoleIdByCodeAsync(string roleCode)
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task<string> GetAnySubsystemRoleCodeAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
    }

    private async Task<int> GetSubsystemRoleIdByCodeAsync(string roleCode)
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task<int> EnsureProjectRoleAssignmentAsync(int projectId, int osobaId, string roleCode)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var roleId = await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .SingleAsync();

        var existingId = await dbContext.ObsazeniProjektu
            .AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.OsobaId == osobaId && x.RoleId == roleId && !x.DatumOdebrani.HasValue)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var entity = new ObsazeniProjektuEntity
        {
            ProjektId = projectId,
            OsobaId = osobaId,
            RoleId = roleId,
            DatumPrirazeni = DateTime.UtcNow
        };

        dbContext.ObsazeniProjektu.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<int> EnsureProjectSubsystemAsync(int projectId, int subsystemId)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var existingId = await dbContext.ProjektSubsystemy
            .AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.SubsystemId == subsystemId && !x.DatumOdebrani.HasValue)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var entity = new ProjektSubsystemEntity
        {
            ProjektId = projectId,
            SubsystemId = subsystemId,
            DatumPrirazeni = DateTime.UtcNow
        };

        dbContext.ProjektSubsystemy.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<int> EnsureProjectSubsystemRoleAssignmentAsync(int projectSubsystemId, int osobaId, string roleCode)
    {
        await using var dbContext = _fixture.CreateDbContext();

        var roleId = await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .Where(x => x.Kod == roleCode)
            .Select(x => x.Id)
            .SingleAsync();

        var existingId = await dbContext.ObsazeniSubsystemuProjektu
            .AsNoTracking()
            .Where(x =>
                x.ProjektSubsystemId == projectSubsystemId &&
                x.OsobaId == osobaId &&
                x.RoleSubsystemuId == roleId &&
                !x.DatumOdebrani.HasValue)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var entity = new ObsazeniSubsystemuProjektuEntity
        {
            ProjektSubsystemId = projectSubsystemId,
            OsobaId = osobaId,
            RoleSubsystemuId = roleId,
            DatumPrirazeni = DateTime.UtcNow
        };

        dbContext.ObsazeniSubsystemuProjektu.Add(entity);
        await dbContext.SaveChangesAsync();
        return entity.Id;
    }
}
