using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthorizationSnapshotTests
{
    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenSuperAdmin()
    {
        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: true,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("anything").Should().BeTrue();
        snapshot.HasPermission("records.edit", projektId: 999).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenGlobalPermissionPresent()
    {
        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(new[] { "people.manage" }, StringComparer.OrdinalIgnoreCase),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("people.manage").Should().BeTrue();
        snapshot.HasPermission("People.Manage").Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenProjectPermissionMatches()
    {
        var perProject = new Dictionary<int, IReadOnlySet<string>>
        {
            [777] = new HashSet<string>(new[] { "records.edit" }, StringComparer.OrdinalIgnoreCase)
        };

        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: perProject,
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("records.edit", projektId: 777).Should().BeTrue();
        snapshot.HasPermission("records.edit", projektId: 888).Should().BeFalse();
        snapshot.HasPermission("records.edit").Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenSubsystemPermissionMatches()
    {
        var perSubsystem = new Dictionary<int, IReadOnlySet<string>>
        {
            [50] = new HashSet<string>(new[] { "records.comment.subsystemlead" }, StringComparer.OrdinalIgnoreCase)
        };

        var snapshot = new AuthorizationSnapshot(
            IsSuperAdmin: false,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: perSubsystem);

        snapshot.HasPermission("records.comment.subsystemlead", subsystemId: 50).Should().BeTrue();
        snapshot.HasPermission("records.comment.subsystemlead", subsystemId: 51).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldReturnFalse_ForUnknownKey()
    {
        var snapshot = new AuthorizationSnapshot(false,
            new HashSet<string>(),
            new Dictionary<int, IReadOnlySet<string>>(),
            new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("nonexistent.key").Should().BeFalse();
    }

    [Fact]
    public void Empty_Snapshot_DenyAll()
    {
        var snapshot = new AuthorizationSnapshot(false,
            new HashSet<string>(),
            new Dictionary<int, IReadOnlySet<string>>(),
            new Dictionary<int, IReadOnlySet<string>>());

        snapshot.HasPermission("records.edit", projektId: 1).Should().BeFalse();
        snapshot.HasPermission("people.manage").Should().BeFalse();
        snapshot.IsSuperAdmin.Should().BeFalse();
    }
}
