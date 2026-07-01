using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Api.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Api.Controllers;

[Collection(ApiSqlCollection.CollectionName)]
public sealed class ProjectsModalsControllerTests
{
    private readonly ApiSqlFixture _fixture;

    public ProjectsModalsControllerTests(ApiSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NewProjectModal_ShouldRenderProjectCreateForm_WhenUserHasPermission()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/NewProjectModal?asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Nový projekt");
        html.Should().Contain("action=\"/Projekty/SaveProject\"", "formulář musí stále směřovat na stejnou routu");
        html.Should().Contain("name=\"Nazev\"");
        html.Should().Contain("name=\"Zkratka\"");
        html.Should().Contain("name=\"Stav\"");
        html.Should().Contain("name=\"PouzivatIdentJednani\"");
    }

    [Fact]
    public async Task NewProjectModal_ShouldReturnForbidden_WhenUserLacksCreatePermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalNewProjectNoPerm");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/NewProjectModal?asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditProjectModal_ShouldRenderExistingProjectValues_WhenUserHasPermission()
    {
        var marker = $"APIMODAL_EDIT_{Guid.NewGuid():N}"[..20];
        var projectId = await _fixture.EnsureProjectAsync(marker);

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
            project.CelyNazev = "Modal edit project";
            project.PouzivatIdentJednani = true;
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/EditProjectModal?id={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Upravit projekt");
        html.Should().Contain($"name=\"Id\" value=\"{projectId}\"");
        html.Should().Contain("name=\"Nazev\" value=\"Modal edit project\"");
        html.Should().Contain($"name=\"Zkratka\" value=\"{marker}\"");
        // Po migraci na <gov-form-switch>: skutečnou form-hodnotu nese hidden input (value="true"),
        // vizuální stav přepínače je `checked` atribut na gov-form-switch.
        html.Should().Contain("name=\"PouzivatIdentJednani\" value=\"true\"");
        Regex.IsMatch(
                html,
                "<gov-form-switch[^>]*data-project-pouzivat-ident-jednani[^>]*checked",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Should().BeTrue("přepínač PouzivatIdentJednani má být zapnutý (checked) pro projekt s hodnotou true");
    }

    [Fact]
    public async Task EditProjectModal_ShouldReturnForbidden_WhenUserLacksEditPermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalEditNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_EDIT_DENY");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/EditProjectModal?id={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EditProjectModal_ShouldReturnNotFound_WhenProjectDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/EditProjectModal?id=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteProjectModal_ShouldRenderProjectData_WhenUserHasPermission()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_DEL");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var project = await dbContext.Projekty.FirstAsync(x => x.Id == projectId);
            project.CelyNazev = "Delete modal project";
            await dbContext.SaveChangesAsync();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/DeleteProjectModal?id={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Smazat projekt");
        html.Should().Contain("action=\"/Projekty/DeleteProject\"");
        html.Should().Contain($"name=\"ProjektId\" value=\"{projectId}\"");
        html.Should().Contain("Delete modal project");
        html.Should().Contain("APIMODAL_DEL");
    }

    [Fact]
    public async Task DeleteProjectModal_ShouldReturnForbidden_WhenUserLacksDeletePermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalDeleteNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_DEL_DENY");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/DeleteProjectModal?id={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteProjectModal_ShouldReturnNotFound_WhenProjectDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/DeleteProjectModal?id=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NewMeetingModal_ShouldRenderNextMeetingNumberAndExistingNumbers_WhenUserHasPermission()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_MEETING");
        await _fixture.CreateMeetingAsync(projectId, "OPEN", 11);
        await _fixture.CreateMeetingAsync(projectId, "OPEN", 13);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Jednani/NewMeetingModal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Nové jednání");
        html.Should().Contain($"name=\"ProjektId\" value=\"{projectId}\"");
        html.Should().Contain("name=\"CisloJednani\"");
        html.Should().Contain("value=\"14\"", "další číslo jednání se má odvodit jako max+1");
        html.Should().Contain("data-existing-meeting-numbers=\"11,13\"");
        html.Should().Contain("action=\"/Jednani/Save\"");
    }

    [Fact]
    public async Task NewMeetingModal_ShouldReturnForbidden_WhenUserLacksMeetingCreatePermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalMeetingNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_MEET_DENY");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Jednani/NewMeetingModal?projektId={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NewMeetingModal_ShouldReturnNotFound_WhenProjectDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Jednani/NewMeetingModal?projektId=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddTeamMemberModal_ShouldRenderMemberForm_WhenUserHasPermission()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_TEAM");
        var candidateId = await _fixture.EnsurePersonAsync("ApiModalTeamCandidate");

        await using (var dbContext = _fixture.CreateDbContext())
        {
            var candidate = await dbContext.Osoby.AsNoTracking().FirstAsync(x => x.Id == candidateId);
            candidate.Email.Should().NotBeNullOrWhiteSpace();
        }

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/AddTeamMemberModal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Přidat člena týmu");
        html.Should().Contain("action=\"/Projekty/SaveTeamMember\"");
        html.Should().Contain($"name=\"ProjektId\" value=\"{projectId}\"");
        html.Should().Contain("name=\"OsobaId\"");
        html.Should().Contain("name=\"Role\"");
        html.Should().Contain($"data-person-picker-search-url=\"/Projekty/SearchProjectMemberCandidates/{projectId}\"");
    }

    [Fact]
    public async Task AddTeamMemberModal_ShouldReturnForbidden_WhenUserLacksTeamManagePermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalTeamNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_TEAM_DENY");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/AddTeamMemberModal?projektId={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddTeamMemberModal_ShouldReturnNotFound_WhenProjectDoesNotExist()
    {
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/AddTeamMemberModal?projektId=999999&asUser={_fixture.AdminOsobaId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignProjectRoleModal_ShouldRenderRoleAssignmentForm_WhenUserHasPermission()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_ASSIGN_ROLE");
        var candidateId = await _fixture.EnsurePersonAsync("ApiModalAssignRoleCandidate");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/AssignProjectRoleModal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Přidat projektovou roli");
        html.Should().Contain("action=\"/Projekty/AssignProjectRole\"");
        html.Should().Contain($"name=\"ProjektId\" value=\"{projectId}\"");
        html.Should().Contain("name=\"OsobaId\"");
        html.Should().Contain("name=\"RoleKod\"");
        html.Should().Contain($"data-person-picker-search-url=\"/Projekty/SearchProjectMemberCandidates/{projectId}\"");
    }

    [Fact]
    public async Task AssignProjectRoleModal_ShouldReturnForbidden_WhenUserLacksTeamManagePermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalAssignRoleNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_ASSIGN_ROLE_DENY");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/AssignProjectRoleModal?projektId={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignProjectSubsystemModal_ShouldRenderSubsystemAssignmentForm_WhenUserHasPermission()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_ASSIGN_SUB");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIMODAL_ASS_SUBSYS", _fixture.AdminOsobaId);

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/AssignProjectSubsystemModal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Přiřadit subsystém projektu");
        html.Should().Contain("action=\"/Projekty/AssignProjectSubsystem\"");
        html.Should().Contain($"name=\"ProjektId\" value=\"{projectId}\"");
        html.Should().Contain("name=\"SubsystemKod\"");

        await using var dbContext = _fixture.CreateDbContext();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == subsystemId)
            .Select(x => x.Kod)
            .SingleAsync();
        html.Should().Contain(subsystemCode);
    }

    [Fact]
    public async Task AssignProjectSubsystemModal_ShouldReturnForbidden_WhenUserLacksTeamManagePermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalAssignSubNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_ASSIGN_SUB_DENY");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/AssignProjectSubsystemModal?projektId={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignProjectSubsystemRoleModal_ShouldRenderSubsystemRoleAssignmentForm_WhenUserHasPermission()
    {
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_ASSIGN_SUB_ROLE");
        var subsystemId = await _fixture.EnsureSubsystemAsync("APIMODAL_SUB_ROLE_SYS", _fixture.AdminOsobaId);
        var projectSubsystemId = await EnsureProjectSubsystemAsync(projectId, subsystemId);
        var candidateId = await _fixture.EnsurePersonAsync("ApiModalAssignSubsystemRoleCandidate");

        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/Projekty/AssignProjectSubsystemRoleModal?projektId={projectId}&asUser={_fixture.AdminOsobaId}");
        var html = await response.Content.ReadAsStringAsync();
        var decodedHtml = WebUtility.HtmlDecode(html);

        response.StatusCode.Should().Be(HttpStatusCode.OK, html);
        decodedHtml.Should().Contain("Přidat roli v subsystému");
        html.Should().Contain("action=\"/Projekty/AssignProjectSubsystemRole\"");
        html.Should().Contain($"name=\"ProjektId\" value=\"{projectId}\"");
        html.Should().Contain("name=\"ProjektSubsystemId\"");
        html.Should().Contain("name=\"OsobaId\"");
        html.Should().Contain("name=\"RoleKod\"");
        html.Should().Contain(projectSubsystemId.ToString());
        html.Should().Contain($"data-person-picker-search-url=\"/Projekty/SearchProjectMemberCandidates/{projectId}\"");
    }

    [Fact]
    public async Task AssignProjectSubsystemRoleModal_ShouldReturnForbidden_WhenUserLacksTeamManagePermission()
    {
        var outsiderId = await _fixture.EnsurePersonAsync("ApiModalAssignSubRoleNoPerm");
        var projectId = await _fixture.EnsureProjectAsync("APIMODAL_ASSIGN_SUB_ROLE_DENY");
        using var client = _fixture.Factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/Projekty/AssignProjectSubsystemRoleModal?projektId={projectId}&asUser={outsiderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
}
