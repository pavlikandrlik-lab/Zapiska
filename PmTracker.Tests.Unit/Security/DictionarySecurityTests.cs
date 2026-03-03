using FluentAssertions;
using PmTracker.Web.Services.Dictionaries;

namespace PmTracker.Tests.Unit.Security;

public sealed class DictionarySecurityTests
{
    [Fact]
    public void CanAccessDictionary_ShouldRequireSuperAdmin_ForScheduleDictionary()
    {
        DictionarySecurityPolicy.CanAccessDictionary("harmonogram-kroky", isSuperAdmin: false).Should().BeFalse();
        DictionarySecurityPolicy.CanAccessDictionary("harmonogram-kroky", isSuperAdmin: true).Should().BeTrue();
    }

    [Fact]
    public void SupportsLocking_ShouldBeFalse_ForSubsystemy()
    {
        DictionarySecurityPolicy.SupportsLocking("subsystemy").Should().BeFalse();
        DictionarySecurityPolicy.CanChangeLockState("subsystemy", isSuperAdmin: true).Should().BeFalse();
    }

    [Fact]
    public void NormalizeRequestedLockState_ShouldDropLock_ForNonSuperAdmin()
    {
        DictionarySecurityPolicy.NormalizeRequestedLockState("typy-ukolu", requestedIsLocked: true, isSuperAdmin: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void NormalizeRequestedLockState_ShouldKeepLock_ForSuperAdmin()
    {
        DictionarySecurityPolicy.NormalizeRequestedLockState("typy-ukolu", requestedIsLocked: true, isSuperAdmin: true)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void CanModifyRow_ShouldDenyLockedRow_ForNonSuperAdmin()
    {
        DictionarySecurityPolicy.CanModifyRow("typy-ukolu", rowIsLocked: true, isSuperAdmin: false).Should().BeFalse();
        DictionarySecurityPolicy.CanModifyRow("typy-ukolu", rowIsLocked: false, isSuperAdmin: false).Should().BeTrue();
    }
}
