using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class ChronologieRebalanceTests
{
    [Fact]
    public void Rebalance_NewBubbleBreaksOrder_SubsequentStepsShift()
    {
        var kroky = new[]
        {
            new StepperKrok(6, Guid.Parse("11111111-1111-1111-1111-111111111106"),
                CurrentBubbleDatum: new DateTime(2026, 3, 1), CurrentBubbleId: 100),
            new StepperKrok(7, Guid.Parse("11111111-1111-1111-1111-111111111107"),
                CurrentBubbleDatum: new DateTime(2026, 4, 1), CurrentBubbleId: 200)
        };
        var available = new[] { new BubleId(300, new DateTime(2026, 6, 1)) };

        var result = ChronologyRebalancer.Rebalance(
            kroky, targetKrokPoradi: 6, newBubbleId: 400,
            newBubbleDatum: new DateTime(2026, 5, 1), availableBubbles: available);

        result.Updates.Should().HaveCount(2);
        result.Updates[0].KrokPoradi.Should().Be(6);
        result.Updates[0].NewBubbleId.Should().Be(400);
        result.Updates[1].KrokPoradi.Should().Be(7);
        result.Updates[1].NewBubbleId.Should().Be(300);
    }

    [Fact]
    public void Rebalance_NoSuccessorAvailable_StepGoesToBuffer()
    {
        var kroky = new[]
        {
            new StepperKrok(6, Guid.NewGuid(), new DateTime(2026, 3, 1), 100),
            new StepperKrok(7, Guid.NewGuid(), new DateTime(2026, 4, 1), 200)
        };
        var available = Array.Empty<BubleId>();

        var result = ChronologyRebalancer.Rebalance(
            kroky, 6, newBubbleId: 400, newBubbleDatum: new DateTime(2026, 5, 1), available);

        result.Updates[0].NewBubbleId.Should().Be(400);
        result.Updates[1].NewBubbleId.Should().BeNull();
    }

    [Fact]
    public void Rebalance_OrderAlreadyOk_NoSubsequentChange()
    {
        // Starší krok dostane bublinu; následující už je OK (datum dál), nic neměníme
        var kroky = new[]
        {
            new StepperKrok(3, Guid.NewGuid(), new DateTime(2026, 1, 1), 50),
            new StepperKrok(6, Guid.NewGuid(), new DateTime(2026, 6, 1), 60)
        };
        var result = ChronologyRebalancer.Rebalance(
            kroky, 3, 400, new DateTime(2026, 2, 1), Array.Empty<BubleId>());

        result.Updates.Should().ContainSingle();
        result.Updates[0].KrokPoradi.Should().Be(3);
        result.Updates[0].NewBubbleId.Should().Be(400);
    }

    [Fact]
    public void Rebalance_StepsBeforeTarget_Untouched()
    {
        var kroky = new[]
        {
            new StepperKrok(3, Guid.NewGuid(), new DateTime(2026, 1, 1), 10),
            new StepperKrok(6, Guid.NewGuid(), new DateTime(2026, 2, 1), 20),
            new StepperKrok(7, Guid.NewGuid(), new DateTime(2026, 3, 1), 30)
        };

        var result = ChronologyRebalancer.Rebalance(
            kroky, 6, 400, new DateTime(2026, 4, 1), Array.Empty<BubleId>());

        result.Updates.Should().HaveCount(2);
        result.Updates.Should().NotContain(u => u.KrokPoradi == 3);
    }

    [Fact]
    public void Rebalance_UsedBubbleIsNotReused()
    {
        var kroky = new[]
        {
            new StepperKrok(6, Guid.NewGuid(), new DateTime(2026, 3, 1), 100),
            new StepperKrok(7, Guid.NewGuid(), new DateTime(2026, 4, 1), 200)
        };
        var available = new[]
        {
            new BubleId(200, new DateTime(2026, 7, 1)), // toto je aktuální K7 bublina — nesmí se znovu použít
            new BubleId(300, new DateTime(2026, 8, 1)),
        };

        var result = ChronologyRebalancer.Rebalance(
            kroky, 6, newBubbleId: 400,
            newBubbleDatum: new DateTime(2026, 5, 1), availableBubbles: available);

        // K7 se posune na bublinu 300 (ne na 200, která je already-used)
        result.Updates.Single(u => u.KrokPoradi == 7).NewBubbleId.Should().Be(300);
    }

    [Fact]
    public void Rebalance_NullArgs_Throws()
    {
        Action act1 = () => ChronologyRebalancer.Rebalance(null!, 1, 1, DateTime.Now, Array.Empty<BubleId>());
        act1.Should().Throw<ArgumentNullException>();

        Action act2 = () => ChronologyRebalancer.Rebalance(Array.Empty<StepperKrok>(), 1, 1, DateTime.Now, null!);
        act2.Should().Throw<ArgumentNullException>();
    }
}
