using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels.Sync;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SdSyncJobAdminHandlerTests
{
    private static PmTrackerDbContext InMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new PmTrackerDbContext(opts);
        db.SdActiveSyncSettings.Add(new SdActiveSyncSettingsEntity
        {
            Id = 1, IsEnabled = false, PeriodMinutes = 60,
            AnchorAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
        db.SdArchiveSyncSettings.Add(new SdArchiveSyncSettingsEntity
        {
            Id = 1, IsEnabled = false, PeriodMinutes = 1440,
            AnchorAt = new DateTimeOffset(2026, 1, 1, 4, 0, 0, TimeSpan.Zero)
        });
        db.SaveChanges();
        return db;
    }

    // ---------- Active handler ----------

    [Fact]
    public void SdActive_JobKey_Is_sd_active()
    {
        using var db = InMemoryDb();
        var sut = new SdActiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdActiveSyncSettingsEntity>>(),
            new ManualTriggerSignal<SdActiveSyncSettingsEntity>(),
            TimeProvider.System);
        sut.JobKey.Should().Be("sd.active");
    }

    [Fact]
    public async Task SdActive_LoadCardAsync_ReturnsCurrentSettings_WithActiveTitle()
    {
        using var db = InMemoryDb();
        var sut = new SdActiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdActiveSyncSettingsEntity>>(),
            new ManualTriggerSignal<SdActiveSyncSettingsEntity>(),
            TimeProvider.System);

        var vm = await sut.LoadCardAsync(canManage: true, CancellationToken.None);

        vm.JobKey.Should().Be("sd.active");
        vm.Title.Should().Contain("aktivní");
        vm.PeriodMinutes.Should().Be(60);
        vm.IsEnabled.Should().BeFalse();
        vm.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task SdActive_SaveAsync_UpdatesSettings_AndSetsAudit_AndSignalsManualTrigger()
    {
        using var db = InMemoryDb();
        var signal = new ManualTriggerSignal<SdActiveSyncSettingsEntity>();
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero));

        var sut = new SdActiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdActiveSyncSettingsEntity>>(),
            signal,
            fakeTime);

        var result = await sut.SaveAsync(new SyncJobSettingsInputModel
        {
            JobKey = "sd.active",
            IsEnabled = true,
            PeriodMinutes = 120,
            AnchorAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)
        }, editorOsobaId: 5, CancellationToken.None);

        result.Should().BeTrue();
        var entity = await db.SdActiveSyncSettings.AsNoTracking().FirstAsync();
        entity.IsEnabled.Should().BeTrue();
        entity.PeriodMinutes.Should().Be(120);
        entity.UpdatedByOsobaId.Should().Be(5);
        entity.UpdatedAt.Should().Be(new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task SdActive_SaveAsync_WrongJobKey_ReturnsFalse()
    {
        using var db = InMemoryDb();
        var sut = new SdActiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdActiveSyncSettingsEntity>>(),
            new ManualTriggerSignal<SdActiveSyncSettingsEntity>(),
            TimeProvider.System);

        var result = await sut.SaveAsync(new SyncJobSettingsInputModel
        {
            JobKey = "ad.periodic", IsEnabled = true, PeriodMinutes = 60, AnchorAt = DateTimeOffset.UtcNow
        }, editorOsobaId: 1, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SdActive_TriggerManualRunAsync_WithinManualFloor_ReturnsNotAccepted()
    {
        using var db = InMemoryDb();
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero));
        var existing = await db.SdActiveSyncSettings.FirstAsync();
        existing.LastRunAt = fakeTime.GetUtcNow().UtcDateTime.AddSeconds(-30);
        await db.SaveChangesAsync();

        var sut = new SdActiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdActiveSyncSettingsEntity>>(),
            new ManualTriggerSignal<SdActiveSyncSettingsEntity>(),
            fakeTime);

        var outcome = await sut.TriggerManualRunAsync(editorOsobaId: 1, CancellationToken.None);

        outcome.Accepted.Should().BeFalse();
        outcome.Message.Should().Contain("Zkus za");
    }

    [Fact]
    public async Task SdActive_TriggerManualRunAsync_WhenNotRunning_Accepted_AndSignalsTrigger()
    {
        using var db = InMemoryDb();
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero));
        var signal = new ManualTriggerSignal<SdActiveSyncSettingsEntity>();
        var runLock = new Mock<ISyncJobRunLock<SdActiveSyncSettingsEntity>>();
        runLock.Setup(x => x.TryAcquire()).Returns(true);

        var sut = new SdActiveSyncJobAdminHandler(db, runLock.Object, signal, fakeTime);

        var outcome = await sut.TriggerManualRunAsync(editorOsobaId: 1, CancellationToken.None);

        outcome.Accepted.Should().BeTrue();
        outcome.Message.Should().Contain("naplánován");
        runLock.Verify(x => x.Release(), Times.Once);
    }

    [Fact]
    public async Task SdActive_TriggerManualRunAsync_WhenAlreadyRunning_NotAccepted()
    {
        using var db = InMemoryDb();
        var runLock = new Mock<ISyncJobRunLock<SdActiveSyncSettingsEntity>>();
        runLock.Setup(x => x.TryAcquire()).Returns(false);

        var sut = new SdActiveSyncJobAdminHandler(
            db, runLock.Object,
            new ManualTriggerSignal<SdActiveSyncSettingsEntity>(),
            TimeProvider.System);

        var outcome = await sut.TriggerManualRunAsync(editorOsobaId: 1, CancellationToken.None);

        outcome.Accepted.Should().BeFalse();
        outcome.Message.Should().Contain("běží");
    }

    // ---------- Archive handler ----------

    [Fact]
    public void SdArchive_JobKey_Is_sd_archive()
    {
        using var db = InMemoryDb();
        var sut = new SdArchiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdArchiveSyncSettingsEntity>>(),
            new ManualTriggerSignal<SdArchiveSyncSettingsEntity>(),
            TimeProvider.System);
        sut.JobKey.Should().Be("sd.archive");
    }

    [Fact]
    public async Task SdArchive_LoadCardAsync_ReturnsDefaults()
    {
        using var db = InMemoryDb();
        var sut = new SdArchiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdArchiveSyncSettingsEntity>>(),
            new ManualTriggerSignal<SdArchiveSyncSettingsEntity>(),
            TimeProvider.System);

        var vm = await sut.LoadCardAsync(canManage: false, CancellationToken.None);

        vm.JobKey.Should().Be("sd.archive");
        vm.Title.Should().Contain("archivní");
        vm.PeriodMinutes.Should().Be(1440);
        vm.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task SdArchive_SaveAsync_Updates_WhenJobKeyMatches()
    {
        using var db = InMemoryDb();
        var sut = new SdArchiveSyncJobAdminHandler(
            db,
            Mock.Of<ISyncJobRunLock<SdArchiveSyncSettingsEntity>>(),
            new ManualTriggerSignal<SdArchiveSyncSettingsEntity>(),
            TimeProvider.System);

        var result = await sut.SaveAsync(new SyncJobSettingsInputModel
        {
            JobKey = "sd.archive", IsEnabled = true, PeriodMinutes = 720,
            AnchorAt = DateTimeOffset.UtcNow
        }, editorOsobaId: 10, CancellationToken.None);

        result.Should().BeTrue();
        var entity = await db.SdArchiveSyncSettings.AsNoTracking().FirstAsync();
        entity.PeriodMinutes.Should().Be(720);
        entity.UpdatedByOsobaId.Should().Be(10);
    }
}
