using FluentAssertions;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class RecordNumberAllocatorTests
{
    [Fact]
    public void FindLowestAvailablePositive_ShouldReturnFirstGap()
    {
        var result = RecordNumberAllocator.FindLowestAvailablePositive([1, 2, 4, 5]);

        result.Should().Be(3);
    }

    [Fact]
    public void FindLowestAvailablePositive_ShouldIgnoreDuplicatesAndNonPositiveValues()
    {
        var result = RecordNumberAllocator.FindLowestAvailablePositive([0, -1, 1, 1, 2, 4]);

        result.Should().Be(3);
    }

    [Fact]
    public void FindLowestAvailablePositive_ShouldReturnOne_WhenSequenceIsEmpty()
    {
        var result = RecordNumberAllocator.FindLowestAvailablePositive([]);

        result.Should().Be(1);
    }
}
