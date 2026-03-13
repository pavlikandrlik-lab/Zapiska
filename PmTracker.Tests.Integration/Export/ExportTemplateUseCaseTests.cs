using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Export;

namespace PmTracker.Tests.Integration.Export;

[Collection(SqlIntegrationCollection.CollectionName)]
public sealed class ExportTemplateUseCaseTests
{
    private readonly SqlIntegrationFixture _fixture;

    public ExportTemplateUseCaseTests(SqlIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BuildMeetingTemplate_ShouldMatchDataStoreDelegation()
    {
        var db = await _fixture.CreateDatabaseAsync("export_use_case_delegate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var fixedTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.Zero));
        var store = IntegrationTestHelper.CreateDataStore(dbContext, fixedTimeProvider);
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, fixedTimeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportUseCaseAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportUseCaseOwner");
        var subsystemLeadId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExportUseCaseLead");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPCASE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPCASE_SYS", adminId);
        var meetingId = await IntegrationTestHelper.CreateMeetingAsync(dbContext, projectId, "OPEN", meetingNumber: 9501);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        await IntegrationTestHelper.EnsureActiveProjectRoleAssignmentAsync(dbContext, projectId, ownerId, ProjectRoleCodes.ProjectOwner);
        await IntegrationTestHelper.EnsureActiveSubsystemRoleAssignmentAsync(dbContext, projectId, subsystemId, subsystemLeadId, SubsystemRoleCodes.Lead);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "ExportUseCaseRecord");

        dbContext.Vyjadreni.Add(new VyjadreniEntity
        {
            ZaznamId = recordId,
            JednaniId = meetingId,
            AutorOsobaId = ownerId,
            TextVyjadreni = "<p>Export use case comment</p>",
            DatumVyjadreni = new DateTime(2026, 7, 2, 9, 0, 0)
        });
        await dbContext.SaveChangesAsync();

        var fromUseCase = useCase.BuildMeetingTemplate(meetingId, currentUser, autoPrint: false);
        var fromDataStore = store.BuildMeetingPrintTemplate(meetingId, currentUser, autoPrint: false);

        fromDataStore.Should().BeEquivalentTo(fromUseCase);
    }

    private sealed class FixedTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
