using FluentAssertions;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class PersonIdentityMatcherTests
{
    private readonly TextNormalizer _normalizer = new();
    private readonly PersonIdentityMatcher _sut;

    public PersonIdentityMatcherTests()
    {
        _sut = new PersonIdentityMatcher(_normalizer);
    }

    [Fact]
    public void NameEquals_ShouldMatch_WithOrWithoutTitle()
    {
        var normalizedWithoutTitle = _normalizer.Normalize("Pavel Andrlik");
        var normalizedWithTitle = _normalizer.Normalize("Ing. Pavel Andrlik");

        _sut.NameEquals("Ing.", "Pavel", "Andrlik", normalizedWithoutTitle).Should().BeTrue();
        _sut.NameEquals("Ing.", "Pavel", "Andrlik", normalizedWithTitle).Should().BeTrue();
    }

    [Fact]
    public void NameContains_ShouldMatchInBothDirections()
    {
        var searchShort = _normalizer.Normalize("Pavel");
        var searchLong = _normalizer.Normalize("Ing. Pavel Andrlik");

        _sut.NameContains("Ing.", "Pavel", "Andrlik", searchShort).Should().BeTrue();
        _sut.NameContains("Ing.", "Pavel", "Andrlik", searchLong).Should().BeTrue();
    }

    [Fact]
    public void EmailChecks_ShouldWorkForEqualsAndContains()
    {
        var full = _normalizer.Normalize("pavel.andrlik@pmtracker.local");
        var part = _normalizer.Normalize("andrlik");

        _sut.EmailEquals("pavel.andrlik@pmtracker.local", full).Should().BeTrue();
        _sut.EmailContains("pavel.andrlik@pmtracker.local", part).Should().BeTrue();
    }
}
