using FluentAssertions;
using PmTracker.Web.Models.Entities;
using Xunit;

namespace PmTracker.Tests.Unit.ExterniOdkaz;

public sealed class ExterniOdkazLastHarvestedAtTests
{
    [Fact]
    public void ZaznamExterniOdkazEntity_ShouldExposeLastHarvestedAt()
    {
        var entity = new ZaznamExterniOdkazEntity
        {
            LastHarvestedAt = new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc)
        };
        entity.LastHarvestedAt.Should().Be(new DateTime(2026, 4, 21, 10, 0, 0, DateTimeKind.Utc));
    }
}
