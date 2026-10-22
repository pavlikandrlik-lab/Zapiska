using FluentAssertions;
using PmTracker.Web.Services.Records;
using Xunit;

namespace PmTracker.Tests.Unit.Records;

/// <summary>Spec 2026-10-22 R11: parametr adresy „seznam“ — aktivni / jednani-{id}.</summary>
public sealed class RecordSequenceRequestTests
{
    [Theory]
    [InlineData("aktivni")]
    [InlineData(" Aktivni ")]
    public void Parse_Active(string value)
    {
        var request = RecordSequenceRequest.Parse(value);
        request.Should().NotBeNull();
        request!.JednaniId.Should().BeNull();
        request.ToQueryValue().Should().Be("aktivni");
    }

    [Fact]
    public void Parse_Meeting_RoundTrips()
    {
        var request = RecordSequenceRequest.Parse("jednani-906");
        request!.JednaniId.Should().Be(906);
        request.ToQueryValue().Should().Be("jednani-906");
        RecordSequenceRequest.ForMeeting(906).Should().Be(request);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("jednani-")]
    [InlineData("jednani-0")]
    [InlineData("jednani--5")]
    [InlineData("jednani-12x")]
    [InlineData("vse")]
    public void Parse_Invalid_IsNull(string? value)
        => RecordSequenceRequest.Parse(value).Should().BeNull("neplatný parametr = stránka bez šipek");
}
