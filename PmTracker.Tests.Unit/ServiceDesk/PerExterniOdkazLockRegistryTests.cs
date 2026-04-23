using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Unit testy pro <see cref="IPerExterniOdkazLockRegistry"/>. Motivace: registry byl dříve
/// static (<c>ClearForTests</c>), což vedlo ke sdílenému state mezi paralelními testy.
/// Instance-based DI registrace umožní testům vytvořit vlastní registry bez šíření state.
/// </summary>
public sealed class PerExterniOdkazLockRegistryTests
{
    [Fact]
    public void GetOrAdd_SameId_ReturnsSameSemaphore()
    {
        var sut = new PerExterniOdkazLockRegistry();

        var sem1 = sut.GetOrAdd(42);
        var sem2 = sut.GetOrAdd(42);

        sem1.Should().BeSameAs(sem2);
    }

    [Fact]
    public void GetOrAdd_DifferentIds_ReturnsDifferentSemaphores()
    {
        var sut = new PerExterniOdkazLockRegistry();

        var sem1 = sut.GetOrAdd(1);
        var sem2 = sut.GetOrAdd(2);

        sem1.Should().NotBeSameAs(sem2);
    }

    [Fact]
    public void SeparateRegistries_DoNotShareLocks()
    {
        var r1 = new PerExterniOdkazLockRegistry();
        var r2 = new PerExterniOdkazLockRegistry();

        var sem1 = r1.GetOrAdd(99);
        var sem2 = r2.GetOrAdd(99);

        sem1.Should().NotBeSameAs(sem2,
            "each registry instance must own its own semaphore pool — no global static state");
    }
}
