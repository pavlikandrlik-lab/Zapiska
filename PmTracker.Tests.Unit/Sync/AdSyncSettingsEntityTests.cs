using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class AdSyncSettingsEntityTests
{
    [Fact]
    public void Entity_ImplementsISyncJobSettings()
    {
        var e = new AdSyncSettingsEntity
        {
            Id = 1,
            IsEnabled = true,
            PeriodMinutes = 360,
            AnchorAt = new DateTimeOffset(2026, 4, 22, 3, 0, 0, TimeSpan.Zero)
        };

        ISyncJobSettings iface = e;
        iface.IsEnabled.Should().BeTrue();
        iface.PeriodMinutes.Should().Be(360);
        iface.AnchorAt.Hour.Should().Be(3);
    }
}
