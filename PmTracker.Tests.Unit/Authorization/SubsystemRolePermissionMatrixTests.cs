using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SubsystemRolePermissionMatrixTests
{
    private static string[] PermissionsFor(string roleKod) =>
        PermissionSeedConfiguration.RoleMappings
            .Where(m => m.RoleKod == roleKod && m.IsAllowed)
            .Select(m => m.ActionKlic)
            .OrderBy(x => x)
            .ToArray();

    [Fact]
    public void VEDOUCI_SUBSYSTEMU_ShouldHaveSubsystemLeadAndCommentsPermissions()
    {
        // Fáze C — Task C2: VEDOUCI dostává comments.* klíče
        PermissionsFor("VEDOUCI_SUBSYSTEMU").Should().BeEquivalentTo(new[]
        {
            "comments.add",
            "comments.delete.own",
            "comments.edit.own",
            "records.comment.subsystemlead"
        });
    }

    [Fact]
    public void ZASTUPCE_VEDOUCIHO_SUBSYSTEMU_ShouldMatchVedouci()
    {
        PermissionsFor("ZASTUPCE_VEDOUCIHO_SUBSYSTEMU")
            .Should().BeEquivalentTo(PermissionsFor("VEDOUCI_SUBSYSTEMU"));
    }

    [Fact]
    public void METODIK_SUBSYSTEMU_ShouldHaveComments()
    {
        // Fáze C — Task C2: METODIK_SUBSYSTEMU dostává komentovací práva
        PermissionsFor("METODIK_SUBSYSTEMU").Should().BeEquivalentTo(new[]
        {
            "comments.add",
            "comments.delete.own",
            "comments.edit.own"
        });
    }
}
