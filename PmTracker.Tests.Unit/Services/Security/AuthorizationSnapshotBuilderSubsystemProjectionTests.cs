using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Services.Security;

/// <summary>
/// TDD tests for MEDIUM-1 fix: subsystem-scoped permissions must also satisfy project-scoped
/// policy checks. A user with only a subsystem role should be able to access project-level
/// features (dashboard, records list, export) for the parent project of their subsystem.
/// </summary>
public sealed class AuthorizationSnapshotBuilderSubsystemProjectionTests
{
    private static PmTrackerDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PmTrackerDbContext(options);
    }

    /// <summary>
    /// Seeds a minimal authz chain: role → permission, lookup role → authz role,
    /// ProjektSubsystem (PS id=10, project id=5), assignment for osobaId=1.
    /// </summary>
    private static async Task SeedSubsystemRoleAsync(
        PmTrackerDbContext db,
        int osobaId = 1,
        int projektId = 5,
        int projektSubsystemId = 10,
        string permissionKey = "records.edit",
        bool subsystemActive = true,
        bool assignmentActive = true)
    {
        db.AuthzRoles.Add(new AuthzRoleEntity
        {
            Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí subsystemu",
            IsActive = true, Scope = RoleScope.Subsystem
        });
        db.AuthzPermissions.Add(new AuthzPermissionEntity
        {
            Id = 200, Klic = permissionKey, Nazev = "Editovat záznamy",
            CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project,
            IsActive = true, IsSystem = true
        });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity
        {
            Id = 300, RoleId = 100, PermissionId = 200,
            ScopeMode = ScopeMode.All, IsAllowed = true
        });
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity
        {
            Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí subsystemu",
            AuthzRoleId = 100
        });
        db.ProjektSubsystemy.Add(new ProjektSubsystemEntity
        {
            Id = projektSubsystemId,
            ProjektId = projektId,
            SubsystemId = 1,
            Poradi = 1,
            DatumOdebrani = subsystemActive ? null : DateTime.UtcNow.AddDays(-5)
        });
        db.Osoby.Add(new OsobaEntity { Id = osobaId, Jmeno = "Test", Prijmeni = "User" });
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1,
            OsobaId = osobaId,
            ProjektSubsystemId = projektSubsystemId,
            RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow,
            DatumOdebrani = assignmentActive ? null : DateTime.UtcNow.AddDays(-1)
        });

        await db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // Test 1: Subsystem role promotes permissions into PerProjectPermissions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SubsystemRole_GrantsProjectLevelPermission()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        await SeedSubsystemRoleAsync(db, osobaId: 1, projektId: 5, projektSubsystemId: 10, permissionKey: "records.edit");

        var builder = new AuthorizationSnapshotBuilder(db);

        // Act
        var snapshot = await builder.BuildAsync(osobaId: 1, ct: CancellationToken.None);

        // Assert — new behavior: subsystem role implies project-level access
        snapshot.HasPermission("records.edit", projektId: 5, subsystemId: null)
            .Should().BeTrue("subsystem role for PS=10 (member of P=5) must grant project-level access");

        // Assert — unchanged behavior: subsystem-scoped check still works
        snapshot.HasPermission("records.edit", projektId: 5, subsystemId: 10)
            .Should().BeTrue("direct subsystem check must still work");

        // Assert — PerProjectPermissions contains the key (promoted)
        snapshot.PerProjectPermissions.Should().ContainKey(5);
        snapshot.PerProjectPermissions[5].Should().Contain("records.edit",
            "subsystem-scoped permission must be promoted into PerProjectPermissions for the parent project");

        // Assert — PerSubsystemPermissions still populated
        snapshot.PerSubsystemPermissions.Should().ContainKey(10);
        snapshot.PerSubsystemPermissions[10].Should().Contain("records.edit",
            "PerSubsystemPermissions must remain populated (unchanged behavior)");
    }

    // -------------------------------------------------------------------------
    // Test 2: Subsystem role does NOT grant access to unrelated projects
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SubsystemRole_DoesNotGrantPermissionToOtherProjects()
    {
        // Arrange: user assigned to subsystem of project 5 only
        await using var db = CreateInMemoryDb();
        await SeedSubsystemRoleAsync(db, osobaId: 1, projektId: 5, projektSubsystemId: 10, permissionKey: "records.edit");

        var builder = new AuthorizationSnapshotBuilder(db);

        // Act
        var snapshot = await builder.BuildAsync(osobaId: 1, ct: CancellationToken.None);

        // Assert — project 99 must remain denied
        snapshot.HasPermission("records.edit", projektId: 99)
            .Should().BeFalse("no subsystem or project assignment exists for project 99");

        snapshot.PerProjectPermissions.Should().NotContainKey(99,
            "PerProjectPermissions must not contain an entry for unrelated project 99");
    }

    // -------------------------------------------------------------------------
    // Test 3: Project role + subsystem role on same project — merged, deduplicated
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProjectRoleAndSubsystemRole_MergeDeduplicated()
    {
        // Arrange: user has both a project role (P=5, records.edit) AND
        //          a subsystem role (PS=10 of P=5, records.edit)
        await using var db = CreateInMemoryDb();

        // --- Authz role shared for project & subsystem (separate role catalog entries) ---
        db.AuthzRoles.Add(new AuthzRoleEntity
        {
            Id = 100, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí subsystemu",
            IsActive = true, Scope = RoleScope.Subsystem
        });
        db.AuthzRoles.Add(new AuthzRoleEntity
        {
            Id = 101, Kod = "ADM_PROJ", Nazev = "Projektový admin",
            IsActive = true, Scope = RoleScope.Project
        });
        db.AuthzPermissions.Add(new AuthzPermissionEntity
        {
            Id = 200, Klic = "records.edit", Nazev = "Editovat záznamy",
            CategoryId = 1, ScopeLevel = PermissionScopeLevel.Project,
            IsActive = true, IsSystem = true
        });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity
        {
            Id = 300, RoleId = 100, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true
        });
        db.AuthzRolePermissions.Add(new AuthzRolePermissionEntity
        {
            Id = 301, RoleId = 101, PermissionId = 200, ScopeMode = ScopeMode.All, IsAllowed = true
        });

        // Lookup role catalog
        db.CiselnikRoliSubsystemu.Add(new CiselnikRoleSubsystemuEntity
        {
            Id = 1, Kod = "VEDOUCI_SUBSYSTEMU", Nazev = "Vedoucí subsystemu", AuthzRoleId = 100
        });
        db.CiselnikRoliProjektu.Add(new CiselnikRoliProjektuEntity
        {
            Id = 1, Kod = "ADM_PROJ", Nazev = "Projektový admin", AuthzRoleId = 101
        });

        // ProjektSubsystem PS=10, project P=5
        db.ProjektSubsystemy.Add(new ProjektSubsystemEntity
        {
            Id = 10, ProjektId = 5, SubsystemId = 1, Poradi = 1
        });

        db.Osoby.Add(new OsobaEntity { Id = 1, Jmeno = "Test", Prijmeni = "User" });

        // Project assignment
        db.ObsazeniProjektu.Add(new ObsazeniProjektuEntity
        {
            Id = 10, OsobaId = 1, ProjektId = 5, RoleId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });
        // Subsystem assignment
        db.ObsazeniSubsystemuProjektu.Add(new ObsazeniSubsystemuProjektuEntity
        {
            Id = 1, OsobaId = 1, ProjektSubsystemId = 10, RoleSubsystemuId = 1,
            DatumPrirazeni = DateTime.UtcNow, DatumOdebrani = null
        });

        await db.SaveChangesAsync();

        var builder = new AuthorizationSnapshotBuilder(db);

        // Act
        var snapshot = await builder.BuildAsync(osobaId: 1, ct: CancellationToken.None);

        // Assert — "records.edit" appears in PerProjectPermissions[5], deduplicated
        snapshot.PerProjectPermissions.Should().ContainKey(5);
        var projectPerms = snapshot.PerProjectPermissions[5];
        projectPerms.Should().Contain("records.edit");
        // HashSet guarantees no duplicates — count check via the set itself
        projectPerms.Count(k => string.Equals(k, "records.edit", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1, "HashSet must deduplicate the same key from both project and subsystem sources");
    }

    // -------------------------------------------------------------------------
    // Test 4: Inactive subsystem — no promotion into PerProjectPermissions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task InactiveSubsystem_IsFiltered()
    {
        // Arrange: the ProjektSubsystem has DatumOdebrani set (removed/inactive)
        await using var db = CreateInMemoryDb();
        await SeedSubsystemRoleAsync(
            db,
            osobaId: 1,
            projektId: 5,
            projektSubsystemId: 10,
            permissionKey: "records.edit",
            subsystemActive: false);   // <-- inactive subsystem

        var builder = new AuthorizationSnapshotBuilder(db);

        // Act
        var snapshot = await builder.BuildAsync(osobaId: 1, ct: CancellationToken.None);

        // Assert — inactive subsystem must not promote any project permissions
        snapshot.PerProjectPermissions.Should().NotContainKey(5,
            "an inactive ProjektSubsystem (DatumOdebrani != null) must not contribute to PerProjectPermissions");
        snapshot.PerSubsystemPermissions.Should().NotContainKey(10,
            "an inactive ProjektSubsystem must not contribute to PerSubsystemPermissions either");
    }
}
