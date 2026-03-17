using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Modules.Settings;

namespace PmTracker.Tests.Unit.Settings;

public sealed class SettingsModalModelFactoryTests
{
    private readonly SettingsModalModelFactory _sut = new();

    [Fact]
    public void BuildPermissionModal_ShouldInsertHistoricalKey_WhenSelectedKeyIsNotInCatalog()
    {
        var panel = BuildPanel(
            permissionCategories:
            [
                new PermissionCategoryViewModel { Id = 1, Kod = "PROJECTS", Nazev = "Projects", SortOrder = 1 },
                new PermissionCategoryViewModel { Id = 2, Kod = "CUSTOM", Nazev = "Custom", SortOrder = 2 }
            ],
            permissions:
            [
                new PermissionViewModel
                {
                    Id = 10,
                    Klic = "legacy.permission",
                    Nazev = "Legacy",
                    CategoryKod = "CUSTOM",
                    ScopeLevel = "PROJECT",
                    IsSystem = false,
                    IsActive = true
                }
            ]);
        var permission = panel.Permissions.Single();

        var model = _sut.BuildPermissionModal(panel, permission, userId: 15, projektId: 20);

        model.IsEdit.Should().BeTrue();
        model.Command.CategoryId.Should().Be(2);
        model.AvailableKeys[0].Value.Should().Be("legacy.permission");
        model.AvailableKeys[0].Label.Should().Contain("historický klíč");
    }

    [Fact]
    public void BuildRolePermissionModal_ShouldUseSortedDefaults_WhenMappingIsNull()
    {
        var panel = BuildPanel(
            roles:
            [
                new RoleViewModel { Id = 5, Kod = "ZZZ", Nazev = "Zeta", Popis = "z", IsSystem = false, IsActive = true },
                new RoleViewModel { Id = 3, Kod = "AAA", Nazev = "Alfa", Popis = "a", IsSystem = false, IsActive = true }
            ],
            permissions:
            [
                new PermissionViewModel { Id = 8, Klic = "x.zzz", Nazev = "Z", CategoryKod = "PROJECTS", ScopeLevel = "PROJECT", IsSystem = false, IsActive = true },
                new PermissionViewModel { Id = 6, Klic = "x.aaa", Nazev = "A", CategoryKod = "PROJECTS", ScopeLevel = "PROJECT", IsSystem = false, IsActive = true }
            ]);

        var model = _sut.BuildRolePermissionModal(panel, mapping: null, roleId: null, permissionId: null, userId: null, projektId: null);

        model.IsEdit.Should().BeFalse();
        model.Command.RoleId.Should().Be(3);
        model.Command.PermissionId.Should().Be(6);
        model.Command.ScopeMode.Should().Be("ALL");
        model.Command.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void ApplyPermissionCatalogDefaults_ShouldSetCategoryAndScope_FromPermissionCatalog()
    {
        var command = new SaveAuthzPermissionCommand
        {
            Klic = PermissionKeys.SettingsManage,
            CategoryId = 0,
            ScopeLevel = "PROJECT"
        };
        var categories = new List<PermissionCategoryViewModel>
        {
            new() { Id = 9, Kod = "SETTINGS", Nazev = "Nastavení", SortOrder = 1 }
        };

        _sut.ApplyPermissionCatalogDefaults(command, categories);

        command.CategoryId.Should().Be(9);
        command.ScopeLevel.Should().Be("GLOBAL");
    }

    private static NastaveniPanelViewModel BuildPanel(
        IReadOnlyList<RoleViewModel>? roles = null,
        IReadOnlyList<PermissionCategoryViewModel>? permissionCategories = null,
        IReadOnlyList<PermissionViewModel>? permissions = null,
        IReadOnlyList<RolePermissionScopeViewModel>? mappings = null,
        IReadOnlyList<UserRoleAssignmentViewModel>? userRoles = null,
        IReadOnlyList<NastaveniProjektItemViewModel>? projekty = null)
    {
        return new NastaveniPanelViewModel
        {
            SectionKey = "role",
            Nazev = "Role",
            Popis = "Panel",
            Role = roles ?? [],
            PermissionCategories = permissionCategories ?? [],
            Permissions = permissions ?? [],
            RolePermissionScopes = mappings ?? [],
            UserRoles = userRoles ?? [],
            EffectivePermissions = new EffectivePermissionsPreviewViewModel
            {
                SelectedUserId = 0,
                SelectedProjectId = null,
                OsobaId = 0,
                Osoba = string.Empty,
                ProjektId = null,
                ProjektNazev = string.Empty,
                AvailableUsers = [],
                AvailableProjects = [],
                Rows = []
            },
            Projekty = projekty ?? []
        };
    }
}
