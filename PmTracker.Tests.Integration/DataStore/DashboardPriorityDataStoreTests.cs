using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class DashboardPriorityDataStoreTests
{
    private static readonly DateTime FixedToday = new(2026, 4, 14, 9, 0, 0, DateTimeKind.Local);
    private readonly SqlIntegrationFixture _fixture;

    public DashboardPriorityDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FullRebuild_ShouldOrderDashboardFocus_ByScoreThenRoleWeight()
    {
        var db = await _fixture.CreateDatabaseAsync("dashboard_priority_focus_order");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new FixedTimeProvider(FixedToday));

        var dashboardUserId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityDashboardUser");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRIOCUS");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "PRIOCUS_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");

        var ownerRecordId = await CreateRunTaskRecordAsync(dbContext, projectId, dashboardUserId, subsystemId, "Owner task", FixedToday.Date.AddDays(5));
        var collaboratorRecordId = await CreateRunTaskRecordAsync(dbContext, projectId, ownerId, subsystemId, "Collaborator task", FixedToday.Date.AddDays(5));
        dbContext.ZaznamSpoluprace.Add(new PmTracker.Web.Models.Entities.ZaznamSpolupraceEntity
        {
            ZaznamId = collaboratorRecordId,
            OsobaId = dashboardUserId
        });
        await dbContext.SaveChangesAsync();

        store.FullRebuildPriorityMatrix();
        var currentUser = IntegrationTestHelper.BuildUser(dashboardUserId, isSuperAdmin: true);

        var model = store.BuildDashboardFocusList(currentUser);

        model.Items.Select(item => item.RecordId)
            .Should()
            .ContainInOrder(ownerRecordId, collaboratorRecordId);
    }

    [Fact]
    public async Task SaveRecord_ShouldCreateAndRemovePriorityRow_WhenCollaborationChanges()
    {
        var db = await _fixture.CreateDatabaseAsync("dashboard_priority_collaboration");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new FixedTimeProvider(FixedToday));

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityRecordOwner");
        var collaboratorId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityCollaborator");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRICOL");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "PRICOL_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, collaboratorId, ProjectRoleCodes.ProjectManager);
        var recordId = await CreateRunTaskRecordAsync(dbContext, projectId, ownerId, subsystemId, "Collaboration trigger", FixedToday.Date.AddDays(7));
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        var updateCommand = await BuildSaveCommandForExistingRecordAsync(dbContext, recordId, projectId, collaboratorId);
        store.SaveRecord(updateCommand, currentUser);

        (await dbContext.ZaznamPriorityUzivatelu.AsNoTracking()
                .AnyAsync(x => x.ZaznamId == recordId && x.OsobaId == collaboratorId))
            .Should()
            .BeTrue();

        updateCommand.VybraniSpolupracovniciIds.Clear();
        store.SaveRecord(updateCommand, currentUser);

        (await dbContext.ZaznamPriorityUzivatelu.AsNoTracking()
                .AnyAsync(x => x.ZaznamId == recordId && x.OsobaId == collaboratorId))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task DeleteRecord_ShouldRemovePriorityRows()
    {
        var db = await _fixture.CreateDatabaseAsync("dashboard_priority_delete");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new FixedTimeProvider(FixedToday));

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityDeleteAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityDeleteOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRIDEL");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "PRIDEL_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");
        var recordId = await CreateRunTaskRecordAsync(dbContext, projectId, ownerId, subsystemId, "Delete me", FixedToday.Date.AddDays(3));
        store.FullRebuildPriorityMatrix();
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);

        (await dbContext.ZaznamPriorityUzivatelu.AsNoTracking()
                .AnyAsync(x => x.ZaznamId == recordId))
            .Should()
            .BeTrue();

        store.DeleteRecord(new DeleteRecordCommand
        {
            ProjektId = projectId,
            ZaznamId = recordId,
            PotvrditSmazani = true
        }, currentUser);

        (await dbContext.ZaznamPriorityUzivatelu.AsNoTracking()
                .AnyAsync(x => x.ZaznamId == recordId))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task SaveRecord_RebuildShouldRollbackWithOuterTransaction()
    {
        var db = await _fixture.CreateDatabaseAsync("dashboard_priority_rollback");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new FixedTimeProvider(FixedToday));

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityRollbackAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "PriorityRollbackOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "PRIRB");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "PRIRB_SUB", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);
        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, "HOST");
        var recordId = await CreateRunTaskRecordAsync(dbContext, projectId, ownerId, subsystemId, "Rollback record", FixedToday.Date.AddDays(10));
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true);
        var updateCommand = await BuildSaveCommandForExistingRecordAsync(dbContext, recordId, projectId, null);
        updateCommand.TerminUkonceni = FixedToday.Date.AddDays(1);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        store.SaveRecord(updateCommand, currentUser);
        await transaction.RollbackAsync();

        await using var verificationDbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var savedRecord = await verificationDbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        savedRecord.DatumUkonceni.Date.Should().Be(FixedToday.Date.AddDays(10));
        (await verificationDbContext.ZaznamPriorityUzivatelu.AsNoTracking()
                .AnyAsync(x => x.ZaznamId == recordId))
            .Should()
            .BeFalse();
    }

    [Fact]
    public async Task FullRebuild_ShouldWriteSuccessState_WhenNoTasksExist()
    {
        var db = await _fixture.CreateDatabaseAsync("dashboard_priority_state_empty");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext, new FixedTimeProvider(FixedToday));

        store.FullRebuildPriorityMatrix();

        var state = await dbContext.ZaznamPriorityRebuildState.AsNoTracking().SingleAsync(x => x.Id == 1);
        state.LastFullRebuildStatus.Should().Be("SUCCESS");
        state.LastFullRebuildTaskCount.Should().Be(0);
    }

    private static async Task<int> CreateRunTaskRecordAsync(
        PmTracker.Web.Data.PmTrackerDbContext dbContext,
        int projectId,
        int ownerId,
        int subsystemId,
        string title,
        DateTime deadline)
    {
        var taskCategoryId = await dbContext.CiselnikKategoriiZaznamu
            .Where(x => x.Kod == RecordCategoryCodes.TaskShort || x.Kod == RecordCategoryCodes.Task)
            .Select(x => x.Id)
            .FirstAsync();
        var runStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.Kod == TaskStateCodes.Run || x.Kod == TaskStateCodes.Open)
            .Select(x => x.Id)
            .FirstAsync();
        var nextNumber = (await dbContext.ProjektoveZaznamy
            .Where(x => x.ProjektId == projectId)
            .Select(x => (int?)x.CisloZaznamu)
            .MaxAsync() ?? 0) + 1;

        dbContext.ProjektoveZaznamy.Add(new PmTracker.Web.Models.Entities.ProjektovyZaznamEntity
        {
            ProjektId = projectId,
            KategorieId = taskCategoryId,
            StavUkoluId = runStateId,
            AktualniTypUkoluId = null,
            CisloZaznamu = nextNumber,
            Nazev = title,
            Cil = $"{title} goal",
            Popis = $"{title} description",
            VlastnikId = ownerId,
            DatumZalozeni = FixedToday.Date,
            DatumUkonceni = deadline.Date,
            SubsystemId = subsystemId
        });
        await dbContext.SaveChangesAsync();

        return await dbContext.ProjektoveZaznamy
            .OrderByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
    }

    private static async Task<SaveRecordCommand> BuildSaveCommandForExistingRecordAsync(
        PmTracker.Web.Data.PmTrackerDbContext dbContext,
        int recordId,
        int projectId,
        int? collaboratorId)
    {
        var record = await dbContext.ProjektoveZaznamy.AsNoTracking().SingleAsync(x => x.Id == recordId);
        var categoryCode = await dbContext.CiselnikKategoriiZaznamu.AsNoTracking()
            .Where(x => x.Id == record.KategorieId)
            .Select(x => x.Kod)
            .FirstAsync();
        var stateCode = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .Where(x => x.Id == record.StavUkoluId)
            .Select(x => x.Kod)
            .FirstAsync();
        var subsystemCode = await dbContext.Subsystemy.AsNoTracking()
            .Where(x => x.Id == record.SubsystemId)
            .Select(x => x.Kod)
            .FirstAsync();

        return new SaveRecordCommand
        {
            Id = record.Id,
            ProjektId = projectId,
            Kategorie = categoryCode,
            Stav = stateCode,
            Nazev = record.Nazev,
            Cil = record.Cil,
            Popis = record.Popis,
            VlastnikId = record.VlastnikId,
            DatumZalozeni = record.DatumZalozeni,
            TerminUkonceni = record.DatumUkonceni,
            Subsystem = subsystemCode,
            CisloZaznamu = record.CisloZaznamu,
            VybraniSpolupracovniciIds = collaboratorId.HasValue ? [collaboratorId.Value] : []
        };
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }
}
