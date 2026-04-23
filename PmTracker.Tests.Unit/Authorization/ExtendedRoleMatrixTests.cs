using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

/// <summary>
/// Smoke testy nad seed matricí rolí — "role obsahuje aspoň očekávané klíče" (nikoli
/// striktní ekvivalence, ta je v <see cref="ProjectRolePermissionMatrixTests"/>
/// a <see cref="SubsystemRolePermissionMatrixTests"/>).
///
/// Po per-action redesignu 2026-04-23: admin role (SUPERADMIN, APP_ADMIN) mají
/// kompletní matrix (76 klíčů), včetně export.* a comments.*.
/// </summary>
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
            "dashboard.view", "export.pdf.projekt", "export.word.projekt",
            "comments.add", "comments.edit.own", "comments.delete.own",
            "search.reindex", "projects.read.all"
        });
    }

    [Fact]
    public void APP_ADMIN_ShouldEqualSuperadminMatrix()
    {
        // Po per-action redesignu: APP_ADMIN = SUPERADMIN na úrovni permission modelu.
        PermissionsFor("APP_ADMIN").Should().BeEquivalentTo(PermissionsFor("SUPERADMIN"));
    }

    [Fact]
    public void VLASTNIK_PROJEKTU_ShouldHaveDashboardExportComments()
    {
        var perms = PermissionsFor("VLASTNIK_PROJEKTU");
        perms.Should().Contain(new[]
        {
            "dashboard.view", "export.pdf.projekt", "export.word.projekt",
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
            "dashboard.view", "export.pdf.projekt", "export.word.projekt",
            "comments.add", "comments.edit.own", "comments.delete.own"
        });
        perms.Should().NotContain("projects.edit");
    }

    [Fact]
    public void HOST_ShouldHaveReadOnlyKeys_DashboardAndExport()
    {
        var perms = PermissionsFor("HOST");
        perms.Should().Contain(new[]
        {
            "dashboard.view", "export.pdf.projekt", "export.word.projekt"
        });
        perms.Should().NotContain(new[] { "comments.add", "records.edit", "meetings.create" },
            "HOST je read-only — nic nepíše");
    }

    [Fact]
    public void GEST_ShouldHaveCommentsAndReadOnly()
    {
        var perms = PermissionsFor("GEST");
        perms.Should().Contain(new[]
        {
            "comments.add", "comments.edit.own", "comments.delete.own",
            "dashboard.view", "export.pdf.projekt", "export.word.projekt"
        });
        perms.Should().NotContain(new[] { "records.edit", "meetings.create", "proposals.accept" },
            "GEST je komentátor, ne editor");
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
