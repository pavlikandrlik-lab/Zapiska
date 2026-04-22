using System.IO;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Authorization;

public sealed class SeedCleanupMigrationTests
{
    [Fact]
    public void CleanupMigration_FileShouldExist()
    {
        var scriptPath = ResolvePath("db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql");
        File.Exists(scriptPath).Should().BeTrue();
    }

    [Fact]
    public void CleanupMigration_ShouldBeIdempotentAndSafe()
    {
        var script = File.ReadAllText(ResolvePath("db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql"));

        script.Should().Contain("authz.role_permissions");
        script.Should().Contain("SET XACT_ABORT ON");
        script.Should().Contain("BEGIN TRANSACTION");
        script.Should().Contain("COMMIT TRANSACTION");

        // Bezpečnostní pojistka: skript nesmí mazat system role, permissions ani superadmins
        script.Should().NotMatchRegex(@"DELETE\s+FROM\s+authz\.roles\b",
            "cleanup skript nesmí mazat role (jen deaktivovat non-system)");
        script.Should().NotMatchRegex(@"DELETE\s+FROM\s+authz\.permissions\b");
        script.Should().NotMatchRegex(@"DELETE\s+FROM\s+authz\.superadmins\b");
    }

    [Fact]
    public void CleanupMigration_ShouldOnlyTouchNonSystemRoles()
    {
        var script = File.ReadAllText(ResolvePath("db_upgrade_1_3_0_cleanup_orphaned_role_permissions.sql"));

        // Seed má is_system=1 pro všechny namapované role (SUPERADMIN, APP_ADMIN, projektové, subsystémové, READ_ALL)
        // Cleanup smí mazat/deaktivovat JEN is_system=0 rows (custom admin-created).
        script.Should().Contain("is_system = 0",
            "cleanup musí filtrovat jen non-system (custom) role");
    }

    [Fact]
    public void SqlStartupValidator_ShouldReferenceCleanupMigration()
    {
        var code = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

        code.Should().Contain("db_upgrade_1_3_0_cleanup_orphaned_role_permissions",
            "validátor musí odkazovat na cleanup migraci v warning message");
    }
}
