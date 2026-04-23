using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ScheduleAddAuthorizationDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ScheduleAddAuthorizationDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordsScheduleAdd_ShouldAllowSubsystemLead_OnAllTasksInAssignedSubsystem_AndDenyOtherSubsystems()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_add_scope_lead");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var leadId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedLead");
        var ownerAId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedLeadOwnerA");
        var ownerBId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedLeadOwnerB");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SCHEDLD");
        var subsystemAId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SCHEDLDA", ownerAId);
        var subsystemBId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SCHEDLDB", ownerBId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemAId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemBId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemAId, leadId, SubsystemRoleCodes.Lead);

        var subsystemTaskA1Id = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerAId, subsystemAId, "U", "SchedLeadA1");
        var subsystemTaskA2Id = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerBId, subsystemAId, "U", "SchedLeadA2");
        var otherSubsystemTaskId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerBId, subsystemBId, "U", "SchedLeadB1");

        var durationTypeId = store.BuildZaznamEdit(subsystemTaskA1Id).HarmonogramBlok.Kroky
            .Select(x => x.TrvaniTypId)
            .First(x => x > 0);

        var leadUser = IntegrationTestHelper.BuildUser(
            leadId,
            isSuperAdmin: false,
            grants: new[] { IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsScheduleCreate, projectId) });

        store.SaveRecord(BuildScheduleOnlyCommand(projectId, subsystemTaskA1Id, durationTypeId, 4), leadUser);
        store.SaveRecord(BuildScheduleOnlyCommand(projectId, subsystemTaskA2Id, durationTypeId, 6), leadUser);

        var deniedAction = () => store.SaveRecord(BuildScheduleOnlyCommand(projectId, otherSubsystemTaskId, durationTypeId, 8), leadUser);
        deniedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nemáte oprávnění*doplňovat harmonogram*");
    }

    [Fact]
    public async Task RecordsScheduleAdd_ShouldAllowSubsystemDeputy_OnAllTasksInAssignedSubsystem_AndDenyOtherSubsystems()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_add_scope_deputy");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var deputyId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedDeputy");
        var ownerAId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedDeputyOwnerA");
        var ownerBId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedDeputyOwnerB");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SCHEDDP");
        var subsystemAId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SCHEDDPA", ownerAId);
        var subsystemBId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SCHEDDPB", ownerBId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemAId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemBId);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemAId, deputyId, SubsystemRoleCodes.DeputyLead);

        var subsystemTaskA1Id = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerAId, subsystemAId, "U", "SchedDeputyA1");
        var subsystemTaskA2Id = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerBId, subsystemAId, "U", "SchedDeputyA2");
        var otherSubsystemTaskId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerBId, subsystemBId, "U", "SchedDeputyB1");

        var durationTypeId = store.BuildZaznamEdit(subsystemTaskA1Id).HarmonogramBlok.Kroky
            .Select(x => x.TrvaniTypId)
            .First(x => x > 0);

        var deputyUser = IntegrationTestHelper.BuildUser(
            deputyId,
            isSuperAdmin: false,
            grants: new[] { IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsScheduleCreate, projectId) });

        store.SaveRecord(BuildScheduleOnlyCommand(projectId, subsystemTaskA1Id, durationTypeId, 3), deputyUser);
        store.SaveRecord(BuildScheduleOnlyCommand(projectId, subsystemTaskA2Id, durationTypeId, 5), deputyUser);

        var deniedAction = () => store.SaveRecord(BuildScheduleOnlyCommand(projectId, otherSubsystemTaskId, durationTypeId, 7), deputyUser);
        deniedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nemáte oprávnění*doplňovat harmonogram*");
    }

    [Fact]
    public async Task RecordsScheduleAdd_ShouldAllowRecordOwner_OnlyOnOwnTasks()
    {
        var db = await _fixture.CreateDatabaseAsync("schedule_add_scope_owner");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedOwner");
        var otherOwnerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "SchedOwnerOther");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "SCHEDOW");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "SCHEDOWA", ownerId);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);

        var ownTaskId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "SchedOwnerOwn");
        var foreignTaskId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, otherOwnerId, subsystemId, "U", "SchedOwnerForeign");

        var durationTypeId = store.BuildZaznamEdit(ownTaskId).HarmonogramBlok.Kroky
            .Select(x => x.TrvaniTypId)
            .First(x => x > 0);

        var ownerUser = IntegrationTestHelper.BuildUser(
            ownerId,
            isSuperAdmin: false,
            grants: new[] { IntegrationTestHelper.AllowProjectPermission(PermissionKeys.ProposalsScheduleCreate, projectId) });

        store.SaveRecord(BuildScheduleOnlyCommand(projectId, ownTaskId, durationTypeId, 9), ownerUser);

        var deniedAction = () => store.SaveRecord(BuildScheduleOnlyCommand(projectId, foreignTaskId, durationTypeId, 11), ownerUser);
        deniedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*Nemáte oprávnění*doplňovat harmonogram*");

        var ownDuration = await dbContext.ZaznamHarmonogramHodnoty.AsNoTracking()
            .Where(x => x.ZaznamId == ownTaskId && x.TypId == durationTypeId)
            .Select(x => (int?)x.HodnotaInt)
            .SingleOrDefaultAsync();
        ownDuration.Should().Be(9);
    }

    private static SaveRecordCommand BuildScheduleOnlyCommand(int projectId, int recordId, int durationTypeId, int value)
    {
        return new SaveRecordCommand
        {
            Id = recordId,
            ProjektId = projectId,
            EditorTab = "schedule",
            HarmonogramHodnoty = new List<SaveRecordHarmonogramValueCommand>
            {
                new()
                {
                    TypId = durationTypeId,
                    Hodnota = value
                }
            }
        };
    }
}
