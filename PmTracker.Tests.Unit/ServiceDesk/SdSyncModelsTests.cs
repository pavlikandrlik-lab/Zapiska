using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using PmTracker.Web.Services.Sync;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class SdSyncModelsTests
{
    [Fact]
    public void SdReactiveHarvestRequest_CarriesExterniOdkazIdAndSource()
    {
        var req = new SdReactiveHarvestRequest(ExterniOdkazId: 42, Source: SdReactiveSource.RecordSave);

        req.ExterniOdkazId.Should().Be(42);
        req.Source.Should().Be(SdReactiveSource.RecordSave);
    }

    [Fact]
    public void SdReactiveHarvestRequest_ImplementsIHasDedupKey_OnExterniOdkazId()
    {
        IHasDedupKey req = new SdReactiveHarvestRequest(ExterniOdkazId: 99, Source: SdReactiveSource.RecordSave);

        req.DedupKey.Should().Be(99);
    }

    [Fact]
    public void SdReactiveHarvestRequest_TwoRequestsSameIdDifferentSource_HaveSameDedupKey()
    {
        var a = new SdReactiveHarvestRequest(42, SdReactiveSource.RecordSave);
        var b = new SdReactiveHarvestRequest(42, SdReactiveSource.ProposalApprove);

        a.DedupKey.Should().Be(b.DedupKey);
    }

    [Fact]
    public void HarvestScope_Enum_HasActiveArchiveAndAll()
    {
        Enum.IsDefined(typeof(HarvestScope), HarvestScope.Active).Should().BeTrue();
        Enum.IsDefined(typeof(HarvestScope), HarvestScope.Archive).Should().BeTrue();
        Enum.IsDefined(typeof(HarvestScope), HarvestScope.All).Should().BeTrue();
    }

    [Fact]
    public void SdHarvestResult_DurationMsComputedFromTimestamps()
    {
        var result = new SdHarvestResult(
            StartedAt: new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc),
            FinishedAt: new DateTime(2026, 4, 22, 10, 0, 5, DateTimeKind.Utc),
            TicketsChecked: 100,
            TicketsSkippedByFingerprint: 80,
            TicketsDrilled: 20,
            BindingsCreated: 5,
            BindingsUpdated: 2,
            ErrorCount: 1,
            Errors: new[]
            {
                new SdHarvestErrorItem(ExterniOdkazId: 7, TicketId: 654321, Reason: "HOT lookup timeout")
            });

        result.DurationMs.Should().Be(5000);
        result.Errors.Should().HaveCount(1);
    }

    [Fact]
    public void SdHarvestResult_Empty_ReturnsZeroedInstance()
    {
        var started = new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc);
        var result = SdHarvestResult.Empty(started);

        result.StartedAt.Should().Be(started);
        result.FinishedAt.Should().Be(started);
        result.DurationMs.Should().Be(0);
        result.TicketsChecked.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }
}
