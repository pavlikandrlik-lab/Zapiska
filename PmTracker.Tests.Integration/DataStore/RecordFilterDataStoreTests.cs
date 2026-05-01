using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;

namespace PmTracker.Tests.Integration.DataStore;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class RecordFilterDataStoreTests
{
    private readonly SqlIntegrationFixture _fixture;

    public RecordFilterDataStoreTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BuildProjektDetail_ShouldPrepareStrictCategoryAndMeetingStateData_ForFiltering()
    {
        var db = await _fixture.CreateDatabaseAsync("record_filters");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var store = IntegrationTestHelper.CreateDataStore(dbContext);

        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "FilterOwner");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "FIL_SUB", ownerId);
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "FILPRJ");

        var infoRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "INFO", "Info");
        var decisionRecordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "ROZHODNUTI", "Decision");

        var draftMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "DRAFT", meetingNumber: 9300);
        var openMeetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9301);

        dbContext.Vyjadreni.Add(new VyjadreniEntity
        {
            ZaznamId = infoRecordId,
            JednaniId = openMeetingId,
            AutorOsobaId = ownerId,
            TextVyjadreni = "Info comment",
            DatumVyjadreni = DateTime.Now
        });

        dbContext.Vyjadreni.Add(new VyjadreniEntity
        {
            ZaznamId = decisionRecordId,
            JednaniId = draftMeetingId,
            AutorOsobaId = ownerId,
            TextVyjadreni = "Decision comment",
            DatumVyjadreni = DateTime.Now
        });

        await dbContext.SaveChangesAsync();

        var recordsTab = store.BuildProjectRecordsTab(projectId);

        recordsTab.Zaznamy.Select(x => x.Summary).Should().Contain(x => x.Id == infoRecordId && x.KategorieKod == "INFO");
        recordsTab.Zaznamy.Select(x => x.Summary).Should().Contain(x => x.Id == decisionRecordId && x.KategorieKod == "ROZHODNUTI");

        var draftStateId = (await dbContext.CiselnikStavuJednani.Where(x => x.Kod == "DRAFT").Select(x => x.Id).FirstAsync()).ToString();
        var openStateId = (await dbContext.CiselnikStavuJednani.Where(x => x.Kod == "OPEN").Select(x => x.Id).FirstAsync()).ToString();

        var meetingCommentStates = store.BuildRecordMeetingCommentStates(projectId);

        meetingCommentStates[infoRecordId].Should().Contain(openStateId);
        meetingCommentStates[infoRecordId].Should().NotContain(draftStateId);

        meetingCommentStates[decisionRecordId].Should().Contain(draftStateId);
        meetingCommentStates[decisionRecordId].Should().NotContain(openStateId);

        recordsTab.FilterShell.StavyJednaniVyjadreni.Select(x => x.Value)
            .Should()
            .Contain([draftStateId, openStateId]);
    }
}
