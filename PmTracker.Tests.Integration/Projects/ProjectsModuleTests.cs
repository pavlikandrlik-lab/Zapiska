using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Projects;
using PmTracker.Web.Modules.Projects.Commands;
using PmTracker.Web.Modules.Projects.Queries;

namespace PmTracker.Tests.Integration.Projects;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ProjectsModuleTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ProjectsModuleTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProjectQueries_ShouldMatchDataStoreDelegation()
    {
        var db = await _fixture.CreateDatabaseAsync("projects_queries_delegate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var projectsDataStore = new ProjectsDataStore(store);
        var queries = new ProjectsQueries(
            new ProjektExistsQueryHandler(projectsDataStore),
            new BuildProjektyListQueryHandler(projectsDataStore),
            new BuildProjektDetailQueryHandler(projectsDataStore),
            new BuildProjectStatusOptionsQueryHandler(projectsDataStore));
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ProjectsQueriesOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRJQRY");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "PRJQRY_SYS", db.AdminOsobaId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ProjectsQueriesRecord");

        var listFromModule = queries.BuildProjektyList();
        var listFromDataStore = store.BuildProjektyList();

        listFromDataStore.Should().BeEquivalentTo(listFromModule);

        var detailFromModule = queries.BuildProjektDetail(projectId);
        var detailFromDataStore = store.BuildProjektDetail(projectId);

        detailFromDataStore.Should().BeEquivalentTo(detailFromModule);

        var statusesFromModule = queries.BuildProjectStatusOptions(currentUser);
        var statusesFromDataStore = store.BuildCiselnikDetail("stavy-projektu", currentUser).Polozky
            .OrderBy(item => item.Nazev, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new LookupOptionViewModel
            {
                Value = item.Kod,
                Label = item.Nazev
            })
            .ToList();

        statusesFromDataStore.Should().BeEquivalentTo(statusesFromModule);
    }

    [Fact]
    public async Task ProjectCommands_ShouldSaveAndSoftDeleteProject()
    {
        var db = await _fixture.CreateDatabaseAsync("projects_commands_delegate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);
        var projectsDataStore = new ProjectsDataStore(store);
        var commands = new ProjectsCommands(
            new SaveProjectCommandHandler(projectsDataStore),
            new SoftDeleteProjectCommandHandler(projectsDataStore),
            new SaveTeamMemberCommandHandler(projectsDataStore),
            new RemoveTeamMemberCommandHandler(projectsDataStore),
            new AssignProjectRoleCommandHandler(projectsDataStore),
            new DeactivateProjectRoleCommandHandler(projectsDataStore),
            new AssignProjectSubsystemCommandHandler(projectsDataStore),
            new DeactivateProjectSubsystemCommandHandler(projectsDataStore),
            new AssignProjectSubsystemRoleCommandHandler(projectsDataStore),
            new DeactivateProjectSubsystemRoleCommandHandler(projectsDataStore));
        var currentUser = IntegrationTestHelper.BuildUser(db.AdminOsobaId, isSuperAdmin: true);

        var activeStatusCode = (await dbContext.CiselnikStavuProjektu
                .AsNoTracking()
                .OrderBy(item => item.Id)
                .Select(item => item.Kod)
                .ToListAsync())
            .First(code => !string.Equals(code, "DELETED", StringComparison.OrdinalIgnoreCase));
        var marker = $"PRJCMD_{Guid.NewGuid():N}"[..20];

        var projectId = commands.SaveProject(new SaveProjectCommand
        {
            Nazev = $"{marker} Project",
            Zkratka = marker,
            Stav = activeStatusCode,
            PouzivatIdentJednani = false
        }, currentUser);

        var createdProject = await dbContext.Projekty
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId);
        createdProject.Should().NotBeNull();
        createdProject!.Zkratka.Should().Be(marker);

        commands.SoftDeleteProject(new SoftDeleteProjectCommand
        {
            ProjektId = projectId
        }, currentUser);

        var deletedStatusId = await dbContext.CiselnikStavuProjektu
            .AsNoTracking()
            .Select(item => new { item.Id, item.Kod, item.Nazev })
            .ToListAsync();
        var expectedDeletedStatusId = deletedStatusId
            .FirstOrDefault(item =>
                string.Equals(item.Kod, "DELETED", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(item.Nazev) && item.Nazev.Contains("smaz", StringComparison.OrdinalIgnoreCase)))
            ?.Id;

        expectedDeletedStatusId.Should().HaveValue();

        var softDeletedProject = await dbContext.Projekty
            .AsNoTracking()
            .FirstAsync(item => item.Id == projectId);

        softDeletedProject.StavId.Should().Be(expectedDeletedStatusId!.Value);
    }
}
