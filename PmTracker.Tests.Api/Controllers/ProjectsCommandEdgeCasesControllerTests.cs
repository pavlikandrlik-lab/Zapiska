using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Data;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectsCommandEdgeCasesControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectsCommandEdgeCasesControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AssignProjectSubsystem_ShouldReturnOperationFailed_WhenSubsystemAlreadyAssigned()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_SUB_DUP");
        var subsystemCode = $"APIDUP_{Guid.NewGuid():N}"[..13];
        var subsystemId = await _fixture.EnsureSubsystemAsync(subsystemCode, _fixture.AdminOsobaId);
        await EnsureProjectSubsystemAsync(projectId, subsystemId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/AssignProjectSubsystem?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("SubsystemKod", subsystemCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("aktivně přiřazen");
    }

    [Fact]
    public async Task AssignProjectSubsystemRole_ShouldReturnOperationFailed_WhenRoleAlreadyAssigned()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_SUBROLE_DUP");
        var subsystemCode = $"APISRDP_{Guid.NewGuid():N}"[..13];
        var subsystemId = await _fixture.EnsureSubsystemAsync(subsystemCode, _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);
        var personId = await _fixture.EnsurePersonAsync("ApiEdgeSubRoleDuplicate");
        var roleCode = await GetAnySubsystemRoleCodeAsync();
        await EnsureProjectSubsystemRoleAssignmentAsync(projectSubsystemId, personId, roleCode);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/AssignProjectSubsystemRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ProjektSubsystemId", projectSubsystemId.ToString()),
                ("OsobaId", personId.ToString()),
                ("RoleKod", roleCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("aktivně přiřazena");
    }

    [Fact]
    public async Task DeactivateProjectSubsystem_ShouldReturnOperationFailed_WhenSubsystemHasActiveRoles()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_SUB_DEACT");
        var subsystemCode = $"APISRLC_{Guid.NewGuid():N}"[..13];
        var subsystemId = await _fixture.EnsureSubsystemAsync(subsystemCode, _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);
        var personId = await _fixture.EnsurePersonAsync("ApiEdgeSubRoleHolder");
        var roleCode = await GetAnySubsystemRoleCodeAsync();
        await EnsureProjectSubsystemRoleAssignmentAsync(projectSubsystemId, personId, roleCode);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/DeactivateProjectSubsystem?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektSubsystemId", projectSubsystemId.ToString()),
                ("projektId", projectId.ToString())));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("aktivní role");
    }

    [Fact]
    public async Task SaveProject_ShouldReturnOperationFailed_WhenUpdatedProjectDoesNotExist()
    {
        var statusCode = await GetActiveProjectStatusCodeAsync();

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveProject?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Id", "999999"),
                ("Nazev", "Missing project update"),
                ("Zkratka", "APIEDGE_MISSING"),
                ("Stav", statusCode)));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("Projekt 999999 nebyl nalezen");
    }

    [Fact]
    public async Task SaveTeamMember_ShouldReturnOperationFailed_WhenRoleCodeDoesNotExist()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_TEAM_BADROLE");
        var personId = await _fixture.EnsurePersonAsync("ApiEdgeTeamBadRole");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = ApiTestHttpHelper.BuildAjaxPost(
            $"/Projekty/SaveTeamMember?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("OsobaId", personId.ToString()),
                ("Role", "ROLE_DOES_NOT_EXIST")));

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var payload = await ApiTestHttpHelper.ReadModalResultAsync(response);
        payload.Ok.Should().BeFalse();
        payload.ErrorCode.Should().Be(AjaxErrorCodes.OperationFailed);
        payload.Message.Should().Contain("nebyla nalezena");
    }

    [Fact]
    public async Task SaveProject_ShouldRedirectToIndex_WhenInvalidModelIsSubmittedWithoutAjax()
    {
        var statusCode = await GetActiveProjectStatusCodeAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            $"/Projekty/SaveProject?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("Nazev", string.Empty),
                ("Zkratka", "APIEDGE_NOAJAX"),
                ("Stav", statusCode)));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/");
    }

    [Fact]
    public async Task DeleteProject_ShouldRedirectToIndex_WhenInvalidModelIsSubmittedWithoutAjax()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_NOAJAX_DEL");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            $"/Projekty/DeleteProject?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("PotvrditSmazani", "false")));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be("/");
    }

    [Fact]
    public async Task SaveTeamMember_ShouldRedirectToDetailTeamTab_WhenInvalidModelIsSubmittedWithoutAjax()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_NOAJAX_TEAM");
        var roleCode = await GetAnyProjectRoleCodeAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            $"/Projekty/SaveTeamMember?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("Role", roleCode)));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/Projekty/Detail/{projectId}");
        response.Headers.Location!.ToString().Should().Contain("tab=tym");
    }

    [Fact]
    public async Task AssignProjectSubsystem_ShouldRedirectToDetailTeamTab_WhenInvalidModelIsSubmittedWithoutAjax()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_NOAJAX_SUB");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            $"/Projekty/AssignProjectSubsystem?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("SubsystemKod", string.Empty)));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/Projekty/Detail/{projectId}");
        response.Headers.Location!.ToString().Should().Contain("tab=tym");
    }

    [Fact]
    public async Task AssignProjectSubsystemRole_ShouldRedirectToDetailTeamTab_WhenInvalidModelIsSubmittedWithoutAjax()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIEDGE_NOAJAX_SUBROLE");
        var subsystemCode = $"APINAJ_{Guid.NewGuid():N}"[..13];
        var subsystemId = await _fixture.EnsureSubsystemAsync(subsystemCode, _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);
        var roleCode = await GetAnySubsystemRoleCodeAsync();
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.PostAsync(
            $"/Projekty/AssignProjectSubsystemRole?asUser={_fixture.AdminOsobaId}",
            ApiTestHttpHelper.BuildForm(
                ("ProjektId", projectId.ToString()),
                ("ProjektSubsystemId", projectSubsystemId.ToString()),
                ("RoleKod", roleCode)));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/Projekty/Detail/{projectId}");
        response.Headers.Location!.ToString().Should().Contain("tab=tym");
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

    private async Task<string> GetAnyProjectRoleCodeAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.CiselnikRoliProjektu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
    }

    private async Task<string> GetAnySubsystemRoleCodeAsync()
    {
        await using var dbContext = _fixture.CreateDbContext();
        return await dbContext.CiselnikRoliSubsystemu.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Kod)
            .FirstAsync();
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
