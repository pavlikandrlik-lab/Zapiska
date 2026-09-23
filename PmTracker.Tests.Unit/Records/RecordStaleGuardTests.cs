using FluentAssertions;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>
/// Spec 2026-09-17 §5.3 — hláška o cizím zápisu musí pojmenovat autora,
/// a bez dohledaného autora nesmí spadnout.
/// </summary>
public sealed class RecordStaleGuardTests
{
    [Fact]
    public void Message_ObsahujeJmenoACas_KdyzJeAutorZnamy()
    {
        var message = RecordStaleMessageBuilder.Build(
            new RecordLastWriter("Novák Jan", new DateTime(2026, 9, 17, 14, 12, 0, DateTimeKind.Utc)));

        message.Should().Contain("Novák Jan");
    }

    [Fact]
    public void Message_JeSrozumitelna_IKdyzAutorNeniZnamy()
    {
        var message = RecordStaleMessageBuilder.Build(null);

        message.Should().NotBeNullOrWhiteSpace();
        message.Should().NotContain("null");
    }

    [Fact]
    public void ErrorCode_JeRecordStale()
    {
        new RecordStaleException("x").ErrorCode.Should().Be("RECORD_STALE");
    }
}
