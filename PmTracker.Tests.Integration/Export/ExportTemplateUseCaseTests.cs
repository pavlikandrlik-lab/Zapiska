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

    [Fact]
    public async Task BuildProjectTemplate_OrdersRecordsWithinSubsystem_ByCategoryThenNumber()
    {
        var db = await _fixture.CreateDatabaseAsync("export_order_category");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var fixedTimeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 8, 30, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, fixedTimeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpOrderAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpOrderOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPORDER");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPORDER_SYS", adminId);
        var currentUser = IntegrationTestHelper.BuildUser(adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var ukol = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "U", "Ukol A");
        var info = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "INFO", "Info B");
        var rozh = await IntegrationTestHelper.EnsureRecordAsync(dbContext, projectId, ownerId, subsystemId, "ROZHODNUTI", "Rozhodnuti C");

        var model = await useCase.BuildProjectTemplateAsync(projectId, currentUser, autoPrint: false);

        var group = model.SubsystemGroups.Single();
        group.Records.Select(r => r.ZaznamId)
            .Where(id => id == ukol || id == info || id == rozh)
            .Should().Equal(new[] { info, rozh, ukol },
                "tisk řadí uvnitř subsystému kategorie primárně (Informace → Rozhodnutí → Úkol)");
    }

    private sealed class FixedTimeProvider(DateTimeOffset localNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => localNow.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Podbarvení ukončených (2026-09-05), rozhodnutí U5: úkol je dnes ukončený, ale
    /// zavřel se až mezi prvním a druhým jednáním. Na starším zápise proto ukončený
    /// být nesmí, na novějším ano. Kdyby se bral dnešní stav, byly by oba true.
    /// </summary>
    [Fact]
    public async Task BuildMeetingTemplate_MarksCompletionByMeetingDate_NotByTodaysState()
    {
        var db = await _fixture.CreateDatabaseAsync("export_completed_by_meeting_date");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 5, 8, 0, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, timeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpDoneAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpDoneOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPDONE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPDONE_SYS", adminId);
        var recordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "ExpDoneRecord");
        var currentUser = IntegrationTestHelper.BuildUser(
            adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var runningStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => !x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var doneStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();

        // Úkol je DNES ukončený a vznikl dávno před oběma jednáními.
        var record = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == recordId);
        record.StavUkoluId = doneStateId;
        record.DatumZalozeni = new DateTime(2026, 1, 5);

        var earlierMeetingId = await IntegrationTestHelper.CreateMeetingAsync(
            dbContext, projectId, "OPEN", meetingNumber: 9601);
        var laterMeetingId = await IntegrationTestHelper.CreateMeetingAsync(
            dbContext, projectId, "OPEN", meetingNumber: 9602);

        // CreateMeetingAsync dává vždy dnešek — data si test nastavuje sám.
        var earlierMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == earlierMeetingId);
        earlierMeeting.DatumPlanovane = new DateTime(2026, 3, 1);
        var laterMeeting = await dbContext.Jednani.FirstAsync(x => x.Id == laterMeetingId);
        laterMeeting.DatumPlanovane = new DateTime(2026, 4, 1);

        // Zavřel se 20.3., tedy mezi oběma jednáními.
        dbContext.ZaznamHistorieStavuZaznamu.Add(new ZaznamHistorieStavuZaznamuEntity
        {
            ZaznamId = recordId,
            PuvodniStav = runningStateId,
            NovyStav = doneStateId,
            DatumZmeny = new DateTime(2026, 3, 20)
        });
        await dbContext.SaveChangesAsync();

        var earlierModel = await useCase.BuildMeetingTemplateAsync(earlierMeetingId, currentUser, autoPrint: false);
        var laterModel = await useCase.BuildMeetingTemplateAsync(laterMeetingId, currentUser, autoPrint: false);

        earlierModel.Zaznamy.Single(item => item.ZaznamId == recordId)
            .IsCompleted.Should().BeFalse("k 1.3. úkol ještě běžel");
        laterModel.Zaznamy.Single(item => item.ZaznamId == recordId)
            .IsCompleted.Should().BeTrue("k 1.4. už byl zavřený");
    }

    /// <summary>
    /// Podbarvení ukončených (2026-09-05): mimo tisk jednání se bere dnešní stav.
    /// </summary>
    [Fact]
    public async Task BuildProjectTemplate_MarksCompletionByCurrentState()
    {
        var db = await _fixture.CreateDatabaseAsync("export_completed_project_variant");
        await using var dbContext = IntegrationTestHelper.CreateDbContext(db.ConnectionString);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 5, 8, 0, 0, TimeSpan.Zero));
        var useCase = IntegrationTestHelper.CreateExportTemplateUseCase(dbContext, timeProvider);

        var adminId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpProjDoneAdmin");
        var ownerId = await IntegrationTestHelper.EnsurePersonAsync(dbContext, "ExpProjDoneOwner");
        var projectId = await IntegrationTestHelper.EnsureProjectAsync(dbContext, "EXPPRJDONE");
        var subsystemId = await IntegrationTestHelper.EnsureSubsystemAsync(dbContext, "EXPPRJDONE_SYS", adminId);
        var doneRecordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "ExpProjDone");
        var runningRecordId = await IntegrationTestHelper.EnsureRecordAsync(
            dbContext, projectId, ownerId, subsystemId, "U", "ExpProjRunning");
        var currentUser = IntegrationTestHelper.BuildUser(
            adminId, isSuperAdmin: true, visibleProjectIds: new[] { projectId });

        var doneStateId = await dbContext.CiselnikStavuUkolu
            .Where(x => x.IsFinal).OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
        var doneRecord = await dbContext.ProjektoveZaznamy.FirstAsync(x => x.Id == doneRecordId);
        doneRecord.StavUkoluId = doneStateId;
        await dbContext.SaveChangesAsync();

        var model = await useCase.BuildProjectTemplateAsync(
            projectId, currentUser, autoPrint: false, filters: null);

        model.Zaznamy.Single(item => item.ZaznamId == doneRecordId).IsCompleted.Should().BeTrue();
        model.Zaznamy.Single(item => item.ZaznamId == runningRecordId).IsCompleted.Should().BeFalse();
    }
}
