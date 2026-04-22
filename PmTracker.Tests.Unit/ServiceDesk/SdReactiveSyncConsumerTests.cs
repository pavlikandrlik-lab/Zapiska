using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Integration-style test — pustí reálný hosted service proti in-process
/// ReactiveSyncQueue a ověří, že request prochází na
/// <see cref="IVyjadreniHarvestService.HarvestSingleTicketAsync"/>.
/// </summary>
public sealed class SdReactiveSyncConsumerTests
{
    [Fact]
    public async Task HandleAsync_DispatchesToHarvestSingleTicketAsync_WithRequestsExterniOdkazId()
    {
        var harvest = new Mock<IVyjadreniHarvestService>();
        harvest.Setup(h => h.HarvestSingleTicketAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(VyjadreniHarvestResult.Empty());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVyjadreniHarvestService>(harvest.Object);
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var queue = new ReactiveSyncQueue<SdReactiveHarvestRequest>();
        var consumer = new SdReactiveSyncConsumer(queue, scopeFactory, NullLogger<SdReactiveSyncConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await consumer.StartAsync(cts.Token);
        await queue.EnqueueAsync(new SdReactiveHarvestRequest(42, SdReactiveSource.RecordSave), cts.Token);

        // Čekej max 1 s na invocation
        var until = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < until)
        {
            try
            {
                harvest.Verify(h => h.HarvestSingleTicketAsync(42, It.IsAny<CancellationToken>()), Times.Once);
                break;
            }
            catch (MockException)
            {
                await Task.Delay(20, cts.Token);
            }
        }

        harvest.Verify(h => h.HarvestSingleTicketAsync(42, It.IsAny<CancellationToken>()), Times.Once);

        await consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_MultipleRequests_AreAllDispatched()
    {
        var harvest = new Mock<IVyjadreniHarvestService>();
        harvest.Setup(h => h.HarvestSingleTicketAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(VyjadreniHarvestResult.Empty());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVyjadreniHarvestService>(harvest.Object);
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var queue = new ReactiveSyncQueue<SdReactiveHarvestRequest>();
        var consumer = new SdReactiveSyncConsumer(queue, scopeFactory, NullLogger<SdReactiveSyncConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await consumer.StartAsync(cts.Token);
        await queue.EnqueueAsync(new SdReactiveHarvestRequest(1, SdReactiveSource.EditorOpen), cts.Token);
        await queue.EnqueueAsync(new SdReactiveHarvestRequest(2, SdReactiveSource.ProposalApprove), cts.Token);
        await queue.EnqueueAsync(new SdReactiveHarvestRequest(3, SdReactiveSource.TabOpen), cts.Token);

        var until = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < until)
        {
            try
            {
                harvest.Verify(h => h.HarvestSingleTicketAsync(1, It.IsAny<CancellationToken>()), Times.Once);
                harvest.Verify(h => h.HarvestSingleTicketAsync(2, It.IsAny<CancellationToken>()), Times.Once);
                harvest.Verify(h => h.HarvestSingleTicketAsync(3, It.IsAny<CancellationToken>()), Times.Once);
                break;
            }
            catch (MockException)
            {
                await Task.Delay(20, cts.Token);
            }
        }

        harvest.Verify(h => h.HarvestSingleTicketAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        harvest.Verify(h => h.HarvestSingleTicketAsync(2, It.IsAny<CancellationToken>()), Times.Once);
        harvest.Verify(h => h.HarvestSingleTicketAsync(3, It.IsAny<CancellationToken>()), Times.Once);

        await consumer.StopAsync(CancellationToken.None);
    }
}
