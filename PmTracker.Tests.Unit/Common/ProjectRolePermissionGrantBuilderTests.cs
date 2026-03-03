using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class ProjectRolePermissionGrantBuilderTests
{
    [Fact]
    public void BuildImplicitProjectRoleGrants_ShouldReturnProjectScopedAdminGrants_ForAdmProj()
    {
        var grants = ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants(
        [
            new ProjectRoleAssignmentGrantSource
            {
                RoleCode = ProjectRoleCodes.ProjectAdmin,
                ProjectId = 17
            }
        ]);

        grants.Should().HaveCount(4);
        grants.Select(x => x.PermissionKey).Should().BeEquivalentTo(
        [
            PermissionKeys.TeamManage,
            PermissionKeys.RecordsEdit,
            PermissionKeys.MeetingsCreate,
            PermissionKeys.MeetingsEdit
        ]);
        grants.Should().OnlyContain(x =>
            x.ScopeLevel == "PROJECT" &&
            x.ScopeMode == "INCLUDE" &&
            x.IsAllowed &&
            x.ProjectIds.SequenceEqual(new[] { 17 }));
    }

    [Fact]
    public void BuildImplicitProjectRoleGrants_ShouldReturnEmpty_ForUnknownRole()
    {
        var grants = ProjectRolePermissionGrantBuilder.BuildImplicitProjectRoleGrants(
        [
            new ProjectRoleAssignmentGrantSource
            {
                RoleCode = "PM",
                ProjectId = 17
            }
        ]);

        grants.Should().BeEmpty();
    }
}
