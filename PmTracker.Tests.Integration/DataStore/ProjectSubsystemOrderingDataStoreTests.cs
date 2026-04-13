using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ProjectSubsystemOrderingDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ProjectSubsystemOrderingDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AssignProjectSubsystem_ShouldAppendNewActiveSubsystemToEnd()
    {
        var db = await _fixture.CreateDatabaseAsync("project_subsystem_order_append");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SubsystemOrderAppendAdmin");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRJORDAPP");
        var subsystemBId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "ORD_B", adminId);
        var subsystemCId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "ORD_C", adminId);
        var subsystemAId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "ORD_A", adminId);

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemBId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemCId);

        store.AssignProjectSubsystem(
            new AssignProjectSubsystemCommand
            {
                ProjektId = projectId,
                SubsystemKod = "ORD_A"
            },
            IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId }));

        var orderedSubsystemIds = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.DatumOdebrani == null)
            .OrderBy(x => x.Poradi)
            .Select(x => x.SubsystemId)
            .ToListAsync();

        orderedSubsystemIds.Should().Equal(subsystemBId, subsystemCId, subsystemAId);
    }

    [Fact]
    public async Task ReorderProjectSubsystem_ShouldSwapOnlyAdjacentActiveSubsystems_AndIgnoreEdgeMoves()
    {
        var db = await _fixture.CreateDatabaseAsync("project_subsystem_order_swap");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SubsystemOrderSwapAdmin");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRJORDSWP");
        var subsystemAId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SWA", adminId);
        var subsystemBId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SWB", adminId);
        var subsystemCId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SWC", adminId);

        var projectSubsystemAId = await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemAId);
        var projectSubsystemBId = await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemBId);
        var projectSubsystemCId = await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemCId);

        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        store.ReorderProjectSubsystem(
            new ReorderProjectSubsystemCommand
            {
                ProjektId = projectId,
                ProjektSubsystemId = projectSubsystemBId,
                Direction = ProjectSubsystemReorderDirections.Up
            },
            currentUser);

        var afterSwap = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.DatumOdebrani == null)
            .OrderBy(x => x.Poradi)
            .Select(x => new { x.SubsystemId, x.Poradi })
            .ToListAsync();

        afterSwap.Select(x => x.SubsystemId).Should().Equal(subsystemBId, subsystemAId, subsystemCId);
        afterSwap.Select(x => x.Poradi).Should().Equal(1, 2, 3);

        store.ReorderProjectSubsystem(
            new ReorderProjectSubsystemCommand
            {
                ProjektId = projectId,
                ProjektSubsystemId = projectSubsystemCId,
                Direction = ProjectSubsystemReorderDirections.Down
            },
            currentUser);

        var afterNoOp = await dbContext.ProjektSubsystemy.AsNoTracking()
            .Where(x => x.ProjektId == projectId && x.DatumOdebrani == null)
            .OrderBy(x => x.Poradi)
            .Select(x => x.SubsystemId)
            .ToListAsync();

        afterNoOp.Should().Equal(subsystemBId, subsystemAId, subsystemCId);
    }

    [Fact]
    public async Task TeamTab_RecordsTab_AndMeetingExport_ShouldRespectProjectSubsystemOrder()
    {
        var db = await _fixture.CreateDatabaseAsync("project_subsystem_order_tabs_export");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SubsystemOrderTabsAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SubsystemOrderTabsOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRJORDTAB");
        var subsystemZId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "ZZZ", adminId);
        var subsystemAId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "AAA", adminId);

        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemZId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemAId);

        var recordInZId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemZId, "U", "OrderZ");
        var recordInAId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemAId, "U", "OrderA");
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9701);

        dbContext.Vyjadreni.AddRange(
            new VyjadreniEntity
            {
                ZaznamId = recordInZId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Komentář Z",
                DatumVyjadreni = new DateTime(2026, 4, 10, 8, 0, 0)
            },
            new VyjadreniEntity
            {
                ZaznamId = recordInAId,
                JednaniId = meetingId,
                AutorOsobaId = ownerId,
                TextVyjadreni = "Komentář A",
                DatumVyjadreni = new DateTime(2026, 4, 10, 8, 5, 0)
            });
        await dbContext.SaveChangesAsync();

        var teamTab = store.BuildProjectTeamTab(projectId);
        teamTab.AktivniSubsystemyProjektu.Select(x => x.Kod).Should().Equal("ZZZ", "AAA");
        teamTab.AktivniSubsystemyProjektu.Select(x => x.Poradi).Should().Equal(1, 2);
        teamTab.AktivniSubsystemyProjektu.Select(x => x.CanMoveUp).Should().Equal(false, true);
        teamTab.AktivniSubsystemyProjektu.Select(x => x.CanMoveDown).Should().Equal(true, false);

        var recordsTab = store.BuildProjectRecordsTab(projectId);
        recordsTab.SkupinyZaznamu.Select(x => x.Kod).Should().Equal("ZZZ", "AAA");

        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });
        var export = store.BuildMeetingPrintTemplate(meetingId, currentUser, autoPrint: false);
        export.SubsystemGroups.Select(x => x.Subsystem).Should().Equal("ZZZ Subsystem", "AAA Subsystem");
    }
}
