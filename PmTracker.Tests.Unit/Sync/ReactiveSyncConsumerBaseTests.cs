using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class ReactiveSyncConsumerBaseTests
{
    private sealed record FakeRequest(int Id) : IHasDedupKey
    {
        public object DedupKey => Id;
    }

    private sealed class FakeConsumer : ReactiveSyncConsumerBase<FakeRequest>
    {
        public List<int> Handled { get; } = new();
        public TaskCompletionSource<bool> FirstHandled { get; } = new();

        public FakeConsumer(IReactiveSyncQueue<FakeRequest> q, IServiceScopeFactory sf)
            : base(q, sf, NullLogger<FakeConsumer>.Instance) { }

        protected override Task HandleAsync(IServiceScope scope, FakeRequest request, CancellationToken ct)
        {
            Handled.Add(request.Id);
            if (Handled.Count == 1) FirstHandled.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StartAsync_HandlesEnqueuedItems()
    {
        var queue = new ReactiveSyncQueue<FakeRequest>();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var consumer = new FakeConsumer(queue, scopeFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await consumer.StartAsync(cts.Token);
        await queue.EnqueueAsync(new FakeRequest(42), cts.Token);

        await consumer.FirstHandled.Task.WaitAsync(TimeSpan.FromSeconds(1), cts.Token);
        consumer.Handled.Should().ContainSingle().Which.Should().Be(42);

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_ContinuesAfterHandlerException()
    {
        var queue = new ReactiveSyncQueue<FakeRequest>();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var consumer = new ThrowingConsumer(queue, scopeFactory);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await consumer.StartAsync(cts.Token);

        await queue.EnqueueAsync(new FakeRequest(1), cts.Token);  // throw
        await queue.EnqueueAsync(new FakeRequest(2), cts.Token);  // success

        await consumer.SecondHandled.Task.WaitAsync(TimeSpan.FromSeconds(1), cts.Token);
        consumer.Handled.Should().Equal(2);

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Consumer_ReleasesDedupSlot_AfterHandling()
    {
        var queue = new ReactiveSyncQueue<FakeRequest>();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var consumer = new FakeConsumer(queue, scopeFactory);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await consumer.StartAsync(cts.Token);
        await queue.EnqueueAsync(new FakeRequest(99), cts.Token);
        await consumer.FirstHandled.Task.WaitAsync(TimeSpan.FromSeconds(1), cts.Token);

        // Poll for the ack to be applied (consumer runs on BG thread, ack happens in finally).
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (queue.PendingCount != 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, cts.Token);
        }

        queue.PendingCount.Should().Be(0,
            "AcknowledgeProcessed in finally clause must have released the dedup slot");

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Consumer_ReleasesDedupSlot_EvenAfterHandlerException()
    {
        var queue = new ReactiveSyncQueue<FakeRequest>();
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var consumer = new ThrowingConsumer(queue, scopeFactory);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await consumer.StartAsync(cts.Token);

        await queue.EnqueueAsync(new FakeRequest(1), cts.Token);  // throws inside handler

        // Wait for the ack to settle (poll-based; handler throws, but finally still runs).
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (queue.PendingCount != 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, cts.Token);
        }

        queue.PendingCount.Should().Be(0);

        await consumer.StopAsync(CancellationToken.None);
    }

    private sealed class ThrowingConsumer : ReactiveSyncConsumerBase<FakeRequest>
    {
        public List<int> Handled { get; } = new();
        public TaskCompletionSource<bool> SecondHandled { get; } = new();

        public ThrowingConsumer(IReactiveSyncQueue<FakeRequest> q, IServiceScopeFactory sf)
            : base(q, sf, NullLogger<ThrowingConsumer>.Instance) { }

        protected override Task HandleAsync(IServiceScope scope, FakeRequest request, CancellationToken ct)
        {
            if (request.Id == 1) throw new InvalidOperationException("fail");
            Handled.Add(request.Id);
            if (Handled.Count == 1) SecondHandled.TrySetResult(true);
            return Task.CompletedTask;
        }
    }
}
