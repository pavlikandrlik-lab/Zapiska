using FluentAssertions;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.Sync;

public sealed class SyncJobRunLockTests
{
    private sealed class DummyJobA : ISyncJobSettings
    {
        public bool IsEnabled { get; set; }
        public int PeriodMinutes { get; set; }
        public DateTimeOffset AnchorAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public string? LastTriggerKind { get; set; }
        public string? LastResultJson { get; set; }
        public bool IsRunning { get; set; }
        public DateTime? RunStartedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByOsobaId { get; set; }
    }

    [Fact]
    public void TryAcquire_FirstCall_ReturnsTrue()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        sut.TryAcquire().Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_SecondCallWithoutRelease_ReturnsFalse()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        sut.TryAcquire();
        sut.TryAcquire().Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_AfterRelease_ReturnsTrue()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        sut.TryAcquire();
        sut.Release();
        sut.TryAcquire().Should().BeTrue();
    }

    [Fact]
    public void Release_WithoutAcquire_DoesNotThrow()
    {
        var sut = new SyncJobRunLock<DummyJobA>();

        var act = () => sut.Release();

        act.Should().NotThrow();
    }
}
