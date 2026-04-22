using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class AuthzSchemaValidatorTests
{
    [Fact]
    public void SqlStartupValidator_ShouldReferenceAuthzRoleScopeUpgrade()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

        code.Should().Contain("db_upgrade_1_2_0_authz_role_scope",
            "validátor musí uživatele informovat o potřebě spustit migraci, pokud chybí sloupec authz.roles.scope");
    }

    [Fact]
    public void SqlUpgradeScript_ForAuthzRoleScope_ShouldExist()
    {
        var scriptPath = ResolvePath("db_upgrade_1_2_0_authz_role_scope.sql");
        File.Exists(scriptPath).Should().BeTrue();
        var script = File.ReadAllText(scriptPath);
        script.Should().Contain("authz.roles");
        script.Should().Contain("scope");
        script.Should().Contain("ck_authz_roles_scope");
    }
}
