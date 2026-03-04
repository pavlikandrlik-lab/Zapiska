using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class MeetingTasksDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public MeetingTasksDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BuildJednaniDetail_ShouldHideEndAndCancelledTasks()
    {
        var db = await _fixture.CreateDatabaseAsync("meeting_tasks_status_filter");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingTaskAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "MeetingTaskOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "MEETTASK");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "MEETTASK_SYS", adminId);

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureProjectSubsystemAsync(dbContext, projectId, subsystemId);

        var runId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "Run");
        var waitId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "Wait");
        var endId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "End");
        var cancelledId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "Cancelled");

        await EnsureTaskStateAsync(dbContext, "RUN", "Běží", isFinal: false);
        await EnsureTaskStateAsync(dbContext, "WAIT", "Čeká", isFinal: false);
        await EnsureTaskStateAsync(dbContext, "END", "Dokončeno", isFinal: true);
        await EnsureTaskStateAsync(dbContext, "CANCELLED", "Zrušeno", isFinal: true);

        var stateIds = await dbContext.CiselnikStavuUkolu.AsNoTracking()
            .ToDictionaryAsync(x => x.Kod, x => x.Id);

        var records = await dbContext.ProjektoveZaznamy
            .Where(x => x.Id == runId || x.Id == waitId || x.Id == endId || x.Id == cancelledId)
            .ToListAsync();

        records.Single(x => x.Id == runId).StavUkoluId = stateIds["RUN"];
        records.Single(x => x.Id == waitId).StavUkoluId = stateIds["WAIT"];
        records.Single(x => x.Id == endId).StavUkoluId = stateIds["END"];
        records.Single(x => x.Id == cancelledId).StavUkoluId = stateIds["CANCELLED"];
        await dbContext.SaveChangesAsync();

        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9710);
        var detail = store.BuildJednaniDetail(meetingId);

        detail.Ukoly.Select(x => x.ZaznamId).Should().Contain(runId);
        detail.Ukoly.Select(x => x.ZaznamId).Should().Contain(waitId);
        detail.Ukoly.Select(x => x.ZaznamId).Should().NotContain(endId);
        detail.Ukoly.Select(x => x.ZaznamId).Should().NotContain(cancelledId);
    }

    private static async Task EnsureTaskStateAsync(PmTrackerDbContext dbContext, string code, string name, bool isFinal)
    {
        var existing = await dbContext.CiselnikStavuUkolu.FirstOrDefaultAsync(x => x.Kod == code);
        if (existing is not null)
        {
            existing.Nazev = name;
            existing.IsFinal = isFinal;
            await dbContext.SaveChangesAsync();
            return;
        }

        dbContext.CiselnikStavuUkolu.Add(new CiselnikStavuUkoluEntity
        {
            Kod = code,
            Nazev = name,
            IsFinal = isFinal,
            IsLocked = false
        });

        await dbContext.SaveChangesAsync();
    }
}
