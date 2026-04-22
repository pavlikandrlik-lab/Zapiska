using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SdSyncSettingsEntityTests
{
    [Fact]
    public void SdActiveSyncSettingsEntity_ImplementsISyncJobSettings()
    {
        var entity = new SdActiveSyncSettingsEntity
        {
            Id = 1,
            IsEnabled = true,
            PeriodMinutes = 60,
            AnchorAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        entity.Should().BeAssignableTo<ISyncJobSettings>();
        ((ISyncJobSettings)entity).IsEnabled.Should().BeTrue();
        ((ISyncJobSettings)entity).PeriodMinutes.Should().Be(60);
    }

    [Fact]
    public void SdActiveSyncSettingsEntity_DefaultPeriodIs60()
    {
        var entity = new SdActiveSyncSettingsEntity();

        entity.PeriodMinutes.Should().Be(60);
    }

    [Fact]
    public void SdArchiveSyncSettingsEntity_ImplementsISyncJobSettings_DefaultPeriodIs1440()
    {
        var entity = new SdArchiveSyncSettingsEntity();

        entity.Should().BeAssignableTo<ISyncJobSettings>();
        entity.PeriodMinutes.Should().Be(1440);
    }

    [Fact]
    public void ZaznamExterniOdkazEntity_ExposesFingerprintColumns()
    {
        var entity = new ZaznamExterniOdkazEntity
        {
            LastKnownHotZaznamDatum = new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc),
            LastKnownMaxVyjadreniId = 12345L,
            LastKnownVyjadreniCount = 7
        };

        entity.LastKnownHotZaznamDatum.Should().Be(new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc));
        entity.LastKnownMaxVyjadreniId.Should().Be(12345L);
        entity.LastKnownVyjadreniCount.Should().Be(7);
    }
}
