using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class ExtendedRoleMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

    [Fact]
    public void SUPERADMIN_ShouldHaveAllNewKeys()
    {
        var perms = PermissionsFor("SUPERADMIN");
        perms.Should().Contain(new[]
        {
            "dashboard.view", "export.pdf", "export.word",
            "comments.add", "comments.edit.own", "comments.delete.own",
            "search.reindex", "projects.read.all"
        });
    }

    [Fact]
    public void APP_ADMIN_ShouldHaveSearchReindexAndReadAllAndDashboard()
    {
        var perms = PermissionsFor("APP_ADMIN");
        perms.Should().Contain(new[] { "search.reindex", "projects.read.all", "dashboard.view" });
        perms.Should().NotContain(new[] { "export.pdf", "export.word", "comments.add" },
            "APP_ADMIN je administrativní role — neexportuje/nekomentuje projekty");
    }

    [Fact]
    public void VLASTNIK_PROJEKTU_ShouldHaveDashboardExportComments()
    {
        var perms = PermissionsFor("VLASTNIK_PROJEKTU");
        perms.Should().Contain(new[]
        {
            "dashboard.view", "export.pdf", "export.word",
            "comments.add", "comments.edit.own", "comments.delete.own"
        });
    }

    [Theory]
    [InlineData("ADM_PROJ")]
    [InlineData("PROJ_MAN")]
    public void ProjektoveAdminRoles_ShouldHaveDashboardExportComments_ButNotProjectsEdit(string roleKod)
    {
        var perms = PermissionsFor(roleKod);
        perms.Should().Contain(new[]
        {
            "dashboard.view", "export.pdf", "export.word",
            "comments.add", "comments.edit.own", "comments.delete.own"
        });
        perms.Should().NotContain("projects.edit");
    }

    [Fact]
    public void HOST_ShouldHaveReadOnlyKeys_DashboardAndExport()
    {
        var perms = PermissionsFor("HOST");
        perms.Should().BeEquivalentTo(new[]
        {
            "dashboard.view",
            "export.pdf",
            "export.word"
        }, "HOST je read-only — vidí dashboard a exportuje, nic nepíše");
    }

    [Fact]
    public void GEST_ShouldHaveCommentsAndReadOnly()
    {
        var perms = PermissionsFor("GEST");
        perms.Should().BeEquivalentTo(new[]
        {
            "comments.add", "comments.edit.own", "comments.delete.own",
            "dashboard.view", "export.pdf", "export.word",
            "records.comment.subsystemlead"
        }, "GEST smí komentovat + read-only");
    }

    [Theory]
    [InlineData("VEDOUCI_SUBSYSTEMU")]
    [InlineData("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU")]
    [InlineData("METODIK_SUBSYSTEMU")]
    public void SubsystemRoles_ShouldHaveComments(string roleKod)
    {
        var perms = PermissionsFor(roleKod);
        perms.Should().Contain(new[]
        {
            "comments.add", "comments.edit.own", "comments.delete.own"
        });
    }
}
