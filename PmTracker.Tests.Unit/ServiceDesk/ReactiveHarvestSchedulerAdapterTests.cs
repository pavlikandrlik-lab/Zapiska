using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class ReactiveHarvestSchedulerAdapterTests
{
    private static PmTrackerDbContext InMemoryDb()
    {
        var opts = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(opts);
    }

    [Fact]
    public async Task ScheduleHarvestAsync_WhenExterniOdkazExists_EnqueuesRequest_WithRecordSaveSource()
    {
        var queue = new Mock<IReactiveSyncQueue<SdReactiveHarvestRequest>>();
        using var db = InMemoryDb();
        db.ZaznamExterniOdkazy.Add(new ZaznamExterniOdkazEntity { Id = 11, ZaznamId = 77, Cislo = "100001" });
        await db.SaveChangesAsync();

        var sut = new ReactiveHarvestSchedulerAdapter(queue.Object, db);

        await sut.ScheduleHarvestAsync(externiOdkazId: 11);

        queue.Verify(q => q.EnqueueAsync(
            It.Is<SdReactiveHarvestRequest>(r =>
                r.ExterniOdkazId == 11 && r.Source == SdReactiveSource.RecordSave),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleHarvestAsync_WhenExterniOdkazDoesNotExist_DoesNothing()
    {
        var queue = new Mock<IReactiveSyncQueue<SdReactiveHarvestRequest>>();
        using var db = InMemoryDb();
        var sut = new ReactiveHarvestSchedulerAdapter(queue.Object, db);

        await sut.ScheduleHarvestAsync(externiOdkazId: 999);

        queue.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ScheduleHarvestForRecordAsync_EnqueuesOneRequestPerExterniOdkaz_WithEditorOpenSource()
    {
        var queue = new Mock<IReactiveSyncQueue<SdReactiveHarvestRequest>>();
        using var db = InMemoryDb();
        db.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 42, Cislo = "100001" },
            new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 42, Cislo = "100002" },
            new ZaznamExterniOdkazEntity { Id = 3, ZaznamId = 99, Cislo = "100003" } // jiný záznam
        );
        await db.SaveChangesAsync();

        var sut = new ReactiveHarvestSchedulerAdapter(queue.Object, db);

        await sut.ScheduleHarvestForRecordAsync(zaznamId: 42);

        queue.Verify(q => q.EnqueueAsync(
            It.Is<SdReactiveHarvestRequest>(r =>
                r.ExterniOdkazId == 1 && r.Source == SdReactiveSource.EditorOpen),
            It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(q => q.EnqueueAsync(
            It.Is<SdReactiveHarvestRequest>(r =>
                r.ExterniOdkazId == 2 && r.Source == SdReactiveSource.EditorOpen),
            It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(q => q.EnqueueAsync(
            It.Is<SdReactiveHarvestRequest>(r => r.ExterniOdkazId == 3),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleHarvestForRecordAsync_SkipsExterniOdkazWithEmptyCislo()
    {
        var queue = new Mock<IReactiveSyncQueue<SdReactiveHarvestRequest>>();
        using var db = InMemoryDb();
        db.ZaznamExterniOdkazy.AddRange(
            new ZaznamExterniOdkazEntity { Id = 1, ZaznamId = 42, Cislo = "100001" },
            new ZaznamExterniOdkazEntity { Id = 2, ZaznamId = 42, Cislo = "" }  // bez čísla
        );
        await db.SaveChangesAsync();

        var sut = new ReactiveHarvestSchedulerAdapter(queue.Object, db);

        await sut.ScheduleHarvestForRecordAsync(zaznamId: 42);

        queue.Verify(q => q.EnqueueAsync(
            It.Is<SdReactiveHarvestRequest>(r => r.ExterniOdkazId == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(q => q.EnqueueAsync(
            It.Is<SdReactiveHarvestRequest>(r => r.ExterniOdkazId == 2),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScheduleHarvestForRecordAsync_WhenNoExterniOdkazy_DoesNothing()
    {
        var queue = new Mock<IReactiveSyncQueue<SdReactiveHarvestRequest>>();
        using var db = InMemoryDb();
        var sut = new ReactiveHarvestSchedulerAdapter(queue.Object, db);

        await sut.ScheduleHarvestForRecordAsync(zaznamId: 42);

        queue.VerifyNoOtherCalls();
    }
}
