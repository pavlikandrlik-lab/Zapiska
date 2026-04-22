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
    public void VEDOUCI_SUBSYSTEMU_ShouldHaveSubsystemLeadPermission()
    {
        PermissionsFor("VEDOUCI_SUBSYSTEMU").Should().BeEquivalentTo(new[]
        {
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
    public void METODIK_SUBSYSTEMU_ShouldBeEmpty_ForNow()
    {
        // Metodik má v Phase A žádný existing permission key.
        // Po přidání comments.* ve Fázi C získá komentovací práva.
        PermissionsFor("METODIK_SUBSYSTEMU").Should().BeEmpty();
    }
}
