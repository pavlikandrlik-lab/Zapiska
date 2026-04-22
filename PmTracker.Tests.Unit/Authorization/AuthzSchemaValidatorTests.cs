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

    [Fact]
    public void SqlStartupValidator_ShouldReferenceLookupRoleAuthzFkUpgrade()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

        code.Should().Contain("db_upgrade_1_2_1_lookup_role_authz_fk",
            "validátor musí uživatele informovat o potřebě spustit migraci, pokud chybí FK sloupce");
    }

    [Fact]
    public void SqlUpgradeScript_ForLookupRoleAuthzFk_ShouldExist()
    {
        var scriptPath = ResolvePath("db_upgrade_1_2_1_lookup_role_authz_fk.sql");
        File.Exists(scriptPath).Should().BeTrue();
        var script = File.ReadAllText(scriptPath);
        script.Should().Contain("ciselnik_roli_projektu");
        script.Should().Contain("ciselnik_roli_subsystemu");
        script.Should().Contain("authz_role_id");
        script.Should().Contain("fk_ciselnik_roli_projektu_authz_role");
        script.Should().Contain("fk_ciselnik_roli_subsystemu_authz_role");
    }
}
