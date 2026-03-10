using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class SubsystemRolePermissionGrantBuilderTests
{
    [Fact]
    public void BuildImplicitSubsystemRoleGrants_ShouldReturnProjectScopedCommentGrant_ForLeadEquivalentRoles()
    {
        var grants = SubsystemRolePermissionGrantBuilder.BuildImplicitSubsystemRoleGrants(
        [
            new SubsystemRoleAssignmentGrantSource
            {
                RoleCode = SubsystemRoleCodes.DeputyLead,
                ProjectId = 17
            },
            new SubsystemRoleAssignmentGrantSource
            {
                RoleCode = SubsystemRoleCodes.Lead,
                ProjectId = 18
            }
        ]);

        grants.Should().HaveCount(2);
        grants.Select(x => x.PermissionKey).Should().OnlyContain(key => key == PermissionKeys.RecordsCommentSubsystemLead);
        grants.Should().OnlyContain(x =>
            x.ScopeLevel == "PROJECT" &&
            x.ScopeMode == "INCLUDE" &&
            x.IsAllowed &&
            x.SourceType == "SUBSYSTEM_ROLE");

        grants.Should().Contain(x => x.ProjectIds.SequenceEqual(new[] { 17 }));
        grants.Should().Contain(x => x.ProjectIds.SequenceEqual(new[] { 18 }));
    }

    [Fact]
    public void BuildImplicitSubsystemRoleGrants_ShouldReturnEmpty_ForUnknownRole()
    {
        var grants = SubsystemRolePermissionGrantBuilder.BuildImplicitSubsystemRoleGrants(
        [
            new SubsystemRoleAssignmentGrantSource
            {
                RoleCode = SubsystemRoleCodes.Methodik,
                ProjectId = 17
            }
        ]);

        grants.Should().BeEmpty();
    }
}
