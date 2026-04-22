using FluentAssertions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class ReactiveSyncQueueTests
{
    private sealed record FakeRequest(int Id) : IHasDedupKey
    {
        public object DedupKey => Id;
    }

    [Fact]
    public async Task EnqueueAsync_Then_ReadOneItem()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();

        await sut.EnqueueAsync(new FakeRequest(1));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await foreach (var item in sut.ReadAllAsync(cts.Token))
        {
            item.Id.Should().Be(1);
            break;
        }
    }

    [Fact]
    public async Task PendingCount_AfterEnqueue_ReportsCount()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();

        await sut.EnqueueAsync(new FakeRequest(1));
        await sut.EnqueueAsync(new FakeRequest(2));
        await sut.EnqueueAsync(new FakeRequest(3));

        sut.PendingCount.Should().Be(3);
    }

    [Fact]
    public async Task EnqueueAsync_PreservesFifoOrder()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();
        await sut.EnqueueAsync(new FakeRequest(1));
        await sut.EnqueueAsync(new FakeRequest(2));
        await sut.EnqueueAsync(new FakeRequest(3));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var collected = new List<int>();
        await foreach (var item in sut.ReadAllAsync(cts.Token))
        {
            collected.Add(item.Id);
            if (collected.Count == 3) break;
        }

        collected.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task EnqueueAsync_DuplicateDedupKey_IsDroppedWhilePending()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();

        await sut.EnqueueAsync(new FakeRequest(42));
        await sut.EnqueueAsync(new FakeRequest(42));  // same DedupKey — should be no-op
        await sut.EnqueueAsync(new FakeRequest(42));  // another duplicate

        sut.PendingCount.Should().Be(1);
    }

    [Fact]
    public async Task AcknowledgeProcessed_ReleasesDedupSlot()
    {
        var sut = new ReactiveSyncQueue<FakeRequest>();

        var first = new FakeRequest(7);
        await sut.EnqueueAsync(first);
        await sut.EnqueueAsync(new FakeRequest(7));  // deduped
        sut.PendingCount.Should().Be(1);

        sut.AcknowledgeProcessed(first);
        sut.PendingCount.Should().Be(0);

        // After ack, a new request with same key must be accepted again
        await sut.EnqueueAsync(new FakeRequest(7));
        sut.PendingCount.Should().Be(1);
    }
}
