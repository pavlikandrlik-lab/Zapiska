using FluentAssertions;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class ApplicationVersionFormatterTests
{
    [Fact]
    public void FormatDisplayVersion_ShouldPreferInformationalVersionWithoutGitSuffix()
    {
        var result = ApplicationVersionFormatter.FormatDisplayVersion("0.3+9f8a7b", new Version(0, 3, 0, 0));

        result.Should().Be("0.3");
    }

    [Fact]
    public void FormatDisplayVersion_ShouldFallbackToMajorMinor_WhenInformationalVersionIsMissing()
    {
        var result = ApplicationVersionFormatter.FormatDisplayVersion(null, new Version(0, 3, 0, 0));

        result.Should().Be("0.3");
    }

    [Fact]
    public void FormatDisplayVersion_ShouldReturnNa_WhenNoVersionMetadataIsAvailable()
    {
        var result = ApplicationVersionFormatter.FormatDisplayVersion(null, null);

        result.Should().Be("n/a");
    }
}
