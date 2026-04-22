using System.IO;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Architecture testy vynucující, že seed je jediný zdroj pravdy pro autorizační model.
/// Pokud se přidá konstanta do PermissionKeys bez odpovídajícího záznamu v seedu —
/// nebo naopak — test selže.
/// </summary>
public sealed class SeedSourceOfTruthTests
{
    [Fact]
    public void Every_PermissionKeys_Constant_MustBe_Seeded()
    {
        // Arrange
        var keysFromDefinitions = PermissionKeys.AllDefinitions
            .Select(d => d.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var keysFromSeed = PermissionSeedConfiguration.Actions
            .Select(a => a.Klic)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Assert — obě sady musí být identické (unordered)
        keysFromDefinitions.Should().BeEquivalentTo(
            keysFromSeed,
            "každý permission key definovaný v PermissionKeys.AllDefinitions musí mít odpovídající záznam " +
            "v PermissionSeedConfiguration.Actions a naopak — seed je zdroj pravdy");
    }

    [Fact]
    public void Every_Seed_Role_MustHave_At_Least_One_Mapping()
    {
        // Arrange
        var roleKodsWithMappings = PermissionSeedConfiguration.RoleMappings
            .Select(m => m.RoleKod)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var orphanRoles = PermissionSeedConfiguration.Roles
            .Where(r => !roleKodsWithMappings.Contains(r.Kod))
            .Select(r => r.Kod)
            .ToList();

        // Assert
        orphanRoles.Should().BeEmpty(
            "každá role v PermissionSeedConfiguration.Roles musí mít alespoň jedno mapování v RoleMappings — " +
            "role bez mapování je pravděpodobně neúmyslný sirotek");
    }

    [Fact]
    public void No_Controller_Should_Compose_Role_In_Runtime()
    {
        // Arrange
        var root = ArchitectureTestBase.RepoRoot();
        var controllersPath = Path.Combine(root, "PmTracker.Web", "Controllers");
        var files = Directory.GetFiles(controllersPath, "*.cs", SearchOption.AllDirectories);

        // Act
        var violations = new List<string>();
        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            if (code.Contains("new AuthzRoleEntity") || code.Contains("new AuthzRolePermissionEntity"))
            {
                violations.Add(file);
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "role a role-permission mappings se vytváří jen přes seed, ne v controllerech");
    }

    [Fact]
    public void No_Service_Should_Compose_Role_In_Runtime()
    {
        // Arrange
        var root = ArchitectureTestBase.RepoRoot();
        var servicesPath = Path.Combine(root, "PmTracker.Web", "Services");
        var files = Directory.GetFiles(servicesPath, "*.cs", SearchOption.AllDirectories);

        // PermissionSeeder a RoleCatalogLinker LEGITIMNĚ vytváří AuthzRoleEntity — jsou seed cesta
        var allowList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PermissionSeeder.cs",
            "RoleCatalogLinker.cs"
        };

        // Act
        var violations = new List<string>();
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            if (allowList.Contains(fileName))
                continue;

            var code = File.ReadAllText(file);
            if (code.Contains("new AuthzRoleEntity") || code.Contains("new AuthzRolePermissionEntity"))
            {
                violations.Add(file);
            }
        }

        // Assert
        violations.Should().BeEmpty(
            "role a role-permission mappings se vytváří jen přes seed (PermissionSeeder/RoleCatalogLinker), " +
            "ne v ostatních services — viz PermissionSeedConfiguration jako zdroj pravdy");
    }
}
