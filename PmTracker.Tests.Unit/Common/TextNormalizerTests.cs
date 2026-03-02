using FluentAssertions;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class TextNormalizerTests
{
    private readonly TextNormalizer _sut = new();

    [Fact]
    public void Normalize_ShouldLowercaseAndRemoveDiacritics()
    {
        var result = _sut.Normalize("  Žluťoučký KŮŇ  ");

        result.Should().Be("zlutoucky kun");
    }

    [Fact]
    public void Normalize_ShouldReturnEmpty_WhenInputIsBlank()
    {
        _sut.Normalize(string.Empty).Should().BeEmpty();
        _sut.Normalize("   ").Should().BeEmpty();
    }

    [Fact]
    public void NormalizeEmail_ShouldTrimOrReturnNull()
    {
        _sut.NormalizeEmail("  user@example.com ").Should().Be("user@example.com");
        _sut.NormalizeEmail("   ").Should().BeNull();
        _sut.NormalizeEmail(null).Should().BeNull();
    }
}
