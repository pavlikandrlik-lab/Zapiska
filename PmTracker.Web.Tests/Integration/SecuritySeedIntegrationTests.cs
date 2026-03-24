using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Web.Tests.Integration;

public sealed class SecuritySeedIntegrationTests
{
    [Fact]
    public void SeededActions_ShouldMatchPermissionCatalogKeys()
    {
        var seededKeys = PermissionSeedConfiguration.Actions
            .Select(item => item.Klic)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var catalogKeys = PermissionKeys.BuildCatalog()
            .Select(item => item.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(catalogKeys, seededKeys);
    }

    [Fact]
    public void SeededRoleMappings_ShouldReferenceKnownRolesAndActions()
    {
        var roleCodes = new HashSet<string>(
            PermissionSeedConfiguration.Roles.Select(role => role.Kod),
            StringComparer.OrdinalIgnoreCase);
        var actionKeys = new HashSet<string>(
            PermissionSeedConfiguration.Actions.Select(action => action.Klic),
            StringComparer.OrdinalIgnoreCase);

        Assert.NotEmpty(PermissionSeedConfiguration.RoleMappings);

        foreach (var mapping in PermissionSeedConfiguration.RoleMappings)
        {
            Assert.True(
                roleCodes.Contains(mapping.RoleKod),
                $"Role mapping odkazuje na neznámou roli '{mapping.RoleKod}'.");
            Assert.True(
                actionKeys.Contains(mapping.ActionKlic),
                $"Role mapping odkazuje na neznámou akci '{mapping.ActionKlic}'.");
        }
    }
}
