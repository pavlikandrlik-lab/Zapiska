using FluentAssertions;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Unit.Security;

public sealed class PermissionKeysTests
{
    [Fact]
    public void IsSupported_ShouldReturnTrue_ForKnownKey()
    {
        PermissionKeys.IsSupported(PermissionKeys.MeetingsNotesSubsystemLead).Should().BeTrue();
    }

    [Fact]
    public void IsSupported_ShouldReturnFalse_ForUnknownOrEmptyKey()
    {
        PermissionKeys.IsSupported(null).Should().BeFalse();
        PermissionKeys.IsSupported(string.Empty).Should().BeFalse();
        PermissionKeys.IsSupported("records.comment.nonexistent").Should().BeFalse();
    }

    [Fact]
    public void BuildLookupOptions_ShouldContainUniqueAndHumanReadableKeys()
    {
        var options = PermissionKeys.BuildLookupOptions();

        options.Should().NotBeEmpty();
        options.Select(x => x.Value)
            .Should()
            .OnlyHaveUniqueItems();

        options.Should().Contain(x => x.Value == PermissionKeys.ProjectsCreate && x.Label.Contains(PermissionKeys.ProjectsCreate, StringComparison.Ordinal));
        options.Should().Contain(x => x.Value == PermissionKeys.MeetingsNotesSubsystemLead && x.Label.Contains("vedouc", StringComparison.OrdinalIgnoreCase));
    }
}
