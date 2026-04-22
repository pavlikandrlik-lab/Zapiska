using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Contract tests pro <see cref="IHarvestScheduler"/> + stub <see cref="NoOpHarvestScheduler"/>.
/// Plán B dodává jen no-op implementaci — skutečný harvest přijde v Plánu C
/// (nahradí NoOp za <c>ReactiveHarvestSchedulerAdapter</c> dle sd-sync-revise plánu).
/// </summary>
public sealed class HarvestSchedulerContractTests
{
    [Fact]
    public async Task NoOpHarvestScheduler_ScheduleHarvestAsync_CompletesWithoutError()
    {
        IHarvestScheduler sut = new NoOpHarvestScheduler();
        await sut.ScheduleHarvestAsync(42);
    }

    [Fact]
    public async Task NoOpHarvestScheduler_ScheduleHarvestForRecordAsync_CompletesWithoutError()
    {
        IHarvestScheduler sut = new NoOpHarvestScheduler();
        await sut.ScheduleHarvestForRecordAsync(123);
    }

    [Fact]
    public async Task NoOpHarvestScheduler_ScheduleHarvestAsync_WithNegativeId_DoesNotThrow()
    {
        IHarvestScheduler sut = new NoOpHarvestScheduler();
        var act = async () => await sut.ScheduleHarvestAsync(-1);
        await act.Should().NotThrowAsync();
    }
}
