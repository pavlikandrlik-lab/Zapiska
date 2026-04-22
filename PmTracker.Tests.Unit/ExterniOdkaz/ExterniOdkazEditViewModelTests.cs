using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniOdkazEditViewModelTests
{
    [Fact]
    public void ExterniOdkazEditViewModel_ShouldExposeLastHarvestedAt()
    {
        var vm = new ExterniOdkazEditViewModel
        {
            LastHarvestedAt = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc)
        };
        vm.LastHarvestedAt.Should().Be(new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
    }
}
