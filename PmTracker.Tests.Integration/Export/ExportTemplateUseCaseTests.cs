using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Tests.Integration.TestInfrastructure;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;

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
    public async Task BuildMeetingTemplate_ShouldBuildMeetingSnapshotWithResolvedMetadata()
    {
        var db = await _fixture.CreateDatabaseAsync("export_use_case_delegate");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var fixedTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.Zero));
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

        var model = await useCase.BuildMeetingTemplateAsync(meetingId, currentUser, autoPrint: false);

        model.ExportVariant.Should().Be("meeting");
        model.AutoPrint.Should().BeFalse();
        model.ProjektId.Should().Be(projectId);
        model.JednaniId.Should().Be(meetingId);
        model.JednaniCislo.Should().Be(9501);
        model.Vytvoril.Should().Be(currentUser.DisplayName);
        model.VytvorenoDne.Should().Be(fixedTimeProvider.GetLocalNow().LocalDateTime);
        model.Zaznamy.Should().ContainSingle(item => item.ZaznamId == recordId);
        model.Zaznamy.Single(item => item.ZaznamId == recordId)
            .Vyjadreni.Should()
            .ContainSingle(comment => comment.Text.Contains("Export use case comment", StringComparison.Ordinal));
    }

    private sealed class FixedTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
