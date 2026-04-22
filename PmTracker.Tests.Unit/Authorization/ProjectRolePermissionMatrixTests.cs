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
        // Fáze C — Task C2: přidány dashboard/export/comments klíče
        var perms = PermissionsFor("VLASTNIK_PROJEKTU");

        perms.Should().BeEquivalentTo(new[]
        {
            "comments.add",
            "comments.delete.own",
            "comments.edit.own",
            "dashboard.view",
            "export.pdf",
            "export.word",
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
        // Fáze C — Task C2: přidány dashboard/export/comments klíče
        var perms = PermissionsFor("ADM_PROJ");

        perms.Should().BeEquivalentTo(new[]
        {
            "comments.add",
            "comments.delete.own",
            "comments.edit.own",
            "dashboard.view",
            "export.pdf",
            "export.word",
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
    public void HOST_ShouldHaveReadOnlyPermissions()
    {
        // Fáze C — Task C2: HOST dostává dashboard.view + export klíče (read-only)
        PermissionsFor("HOST").Should().BeEquivalentTo(new[]
        {
            "dashboard.view",
            "export.pdf",
            "export.word"
        });
    }

    [Fact]
    public void GEST_ShouldHaveCommentsAndReadOnly()
    {
        // Fáze C — Task C2: GEST dostává comments.* + dashboard/export klíče
        PermissionsFor("GEST").Should().BeEquivalentTo(new[]
        {
            "comments.add",
            "comments.delete.own",
            "comments.edit.own",
            "dashboard.view",
            "export.pdf",
            "export.word",
            "records.comment.subsystemlead"
        });
    }
}
