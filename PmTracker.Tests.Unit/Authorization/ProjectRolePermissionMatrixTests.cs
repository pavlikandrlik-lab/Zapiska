using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ProjectRolePermissionMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

    [Fact]
    public void VLASTNIK_PROJEKTU_ShouldHaveFullProjectPermissions()
    {
        var perms = PermissionsFor("VLASTNIK_PROJEKTU");

        perms.Should().BeEquivalentTo(new[]
        {
            "meetings.create",
            "meetings.edit",
            "projects.edit",
            "records.comment.subsystemlead",
            "records.edit",
            "records.schedule.add",
            "records.schedule.edit",
            "team.manage"
        });
    }

    [Fact]
    public void ADM_PROJ_ShouldHaveProjectAdminMinusProjectsEdit()
    {
        var perms = PermissionsFor("ADM_PROJ");

        perms.Should().BeEquivalentTo(new[]
        {
            "meetings.create",
            "meetings.edit",
            "records.comment.subsystemlead",
            "records.edit",
            "records.schedule.add",
            "records.schedule.edit",
            "team.manage"
        });
    }

    [Fact]
    public void PROJ_MAN_ShouldMatchAdmProj()
    {
        PermissionsFor("PROJ_MAN").Should().BeEquivalentTo(PermissionsFor("ADM_PROJ"));
    }

    [Fact]
    public void HOST_ShouldHaveNoWritePermissions()
    {
        // HOST is read-only; dashboard.view / export.* keys come in Phase C.
        PermissionsFor("HOST").Should().BeEmpty();
    }

    [Fact]
    public void GEST_ShouldHaveCommentsOnly()
    {
        // Comments.* keys arrive in Phase C. In Phase A we only have records.comment.subsystemlead.
        PermissionsFor("GEST").Should().BeEquivalentTo(new[]
        {
            "records.comment.subsystemlead"
        });
    }
}
