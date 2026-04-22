using FluentAssertions;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class RoleSeedCoverageTests
{
    [Fact]
    public void Roles_ShouldContainAllProjectScopeRoles()
    {
        var projectRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope == "PROJECT")
            .Select(r => r.Kod)
            .OrderBy(x => x)
            .ToList();

        projectRoles.Should().BeEquivalentTo(new[]
        {
            "ADM_PROJ", "GEST", "HOST", "PROJ_MAN", "VLASTNIK_PROJEKTU"
        });
    }

    [Fact]
    public void Roles_ShouldContainAllSubsystemScopeRoles()
    {
        var subsystemRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope == "SUBSYSTEM")
            .Select(r => r.Kod)
            .OrderBy(x => x)
            .ToList();

        subsystemRoles.Should().BeEquivalentTo(new[]
        {
            "METODIK_SUBSYSTEMU", "VEDOUCI_SUBSYSTEMU", "ZASTUPCE_VEDOUCIHO_SUBSYSTEMU"
        });
    }

    [Fact]
    public void Roles_GlobalScope_ShouldContainSuperAdminAndAppAdmin()
    {
        var globalRoles = PermissionSeedConfiguration.Roles
            .Where(r => r.Scope == "GLOBAL")
            .Select(r => r.Kod)
            .OrderBy(x => x)
            .ToList();

        globalRoles.Should().Contain(new[] { "APP_ADMIN", "SUPERADMIN" });
    }
}
