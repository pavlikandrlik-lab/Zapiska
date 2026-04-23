using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Security;

public sealed class CurrentUserContextPermissionTests
{
    [Fact]
    public void HasPermission_ShouldAlwaysReturnTrue_ForSuperAdmin()
    {
        var user = BuildUser(
            isSuperAdmin: true,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.ProjectsCreate,
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = false,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermission("anything.random", 999).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ShouldReturnFalse_WhenNoMatchingGrantExists()
    {
        var user = BuildUser(isSuperAdmin: false);

        user.HasPermission(PermissionKeys.ProjectsCreate).Should().BeFalse();
        user.HasPermission(PermissionKeys.ProjectsCreate, 1).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldTreatProjectNull_AsGlobalOrAllOnly()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            visibleProjectIds: [7],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.TeamMemberAdd,
                ScopeLevel = "PROJECT",
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = new[] { 7 }
            });

        user.HasPermission(PermissionKeys.TeamMemberAdd).Should().BeFalse("INCLUDE bez konkrétního projektu se nepočítá");
        user.HasPermission(PermissionKeys.TeamMemberAdd, 7).Should().BeTrue();
        user.HasPermission(PermissionKeys.TeamMemberAdd, 8).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldHonorAllScope_ForAnyProject()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            visibleProjectIds: [1234],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.MeetingsEdit,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermission(PermissionKeys.MeetingsEdit).Should().BeTrue();
        user.HasPermission(PermissionKeys.MeetingsEdit, 1234).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ShouldIgnoreDeniedGrants_WhenNoAllowExists()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.SettingsView,
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = false,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermission(PermissionKeys.SettingsView).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldReturnTrue_WhenAnyAllowGrantMatches()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants:
            [
                new PermissionGrantViewModel
                {
                    PermissionKey = PermissionKeys.RecordsEdit,
                    ScopeLevel = "PROJECT",
                    ScopeMode = "INCLUDE",
                    IsAllowed = true,
                    ProjectIds = new[] { 2, 3 }
                },
                new PermissionGrantViewModel
                {
                    PermissionKey = PermissionKeys.RecordsEdit,
                    ScopeLevel = "PROJECT",
                    ScopeMode = "INCLUDE",
                    IsAllowed = false,
                    ProjectIds = new[] { 2 }
                }
            ]);

        user.HasPermission(PermissionKeys.RecordsEdit, 3).Should().BeTrue();
    }

    [Fact]
    public void CanAccessProject_ShouldReturnTrue_ForVisibleProject()
    {
        var user = BuildUser(isSuperAdmin: false, visibleProjectIds: [5, 7]);

        user.CanAccessProject(7).Should().BeTrue();
    }

    [Fact]
    public void CanAccessProject_ShouldReturnTrue_ForMatchingProjectGrant()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsEdit,
                ScopeLevel = "PROJECT",
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = [3]
            });

        user.CanAccessProject(3).Should().BeTrue();
    }

    [Fact]
    public void CanAccessProject_ShouldIgnoreProjectsCreateGrant()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.ProjectsCreate,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.CanAccessProject(3).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldStillReturnTrue_ForGlobalPermissionWithoutProjectId()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.SettingsView,
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermission(PermissionKeys.SettingsView).Should().BeTrue();
    }

    [Fact]
    public void HasPermission_ShouldReturnFalse_ForProjectWritePermission_WhenProjectIsDeleted()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            visibleProjectIds: [7],
            deletedProjectIds: [7],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.TeamMemberAdd,
                ScopeLevel = "PROJECT",
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = [7]
            });

        user.CanAccessProject(7).Should().BeTrue();
        user.HasPermission(PermissionKeys.TeamMemberAdd, 7).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldReturnFalse_ForDeletedProject_EvenForSuperAdmin()
    {
        var user = BuildUser(
            isSuperAdmin: true,
            visibleProjectIds: [9],
            deletedProjectIds: [9],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsEdit,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.CanAccessProject(9).Should().BeTrue();
        user.HasPermission(PermissionKeys.RecordsEdit, 9).Should().BeFalse();
    }

    [Fact]
    public void HasPermissionPrefix_ShouldAlwaysReturnTrue_ForSuperAdmin()
    {
        var user = BuildUser(isSuperAdmin: true);

        user.HasPermissionPrefix(PermissionKeys.SettingsPrefix).Should().BeTrue();
    }

    [Fact]
    public void HasPermissionPrefix_ShouldReturnFalse_WhenNoMatchingGrantExists()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.ProjectsCreate,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermissionPrefix(PermissionKeys.PeoplePrefix).Should().BeFalse();
    }

    [Fact]
    public void HasPermissionPrefix_ShouldReturnTrue_WhenAllowedGrantMatchesPrefix()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.SettingsView,
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermissionPrefix(PermissionKeys.SettingsPrefix).Should().BeTrue();
    }

    [Fact]
    public void HasPermissionPrefix_ShouldIgnoreDeniedGrant_WhenNoAllowedGrantMatches()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.SettingsRolesAssign,
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = false,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermissionPrefix(PermissionKeys.SettingsPrefix).Should().BeFalse();
    }

    [Fact]
    public void HasPermissionPrefix_ShouldBeCaseInsensitive()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = "People.Manage",
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermissionPrefix(PermissionKeys.PeoplePrefix).Should().BeTrue();
    }

    [Fact]
    public void HasPermissionPrefix_ShouldAcceptPrefixWithoutTrailingDot()
    {
        var user = BuildUser(
            isSuperAdmin: false,
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.SettingsRolesAssign,
                ScopeLevel = "GLOBAL",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        user.HasPermissionPrefix("settings").Should().BeTrue();
    }

    /// <summary>
    /// Převede pole <see cref="PermissionGrantViewModel"/> na <see cref="AuthorizationSnapshot"/>
    /// pro testy, které ověřují chování metod delegujících na snapshot.
    /// Mapování:
    ///   IsAllowed=false → klíč se do snapshotu nepromítne (snapshot ukládá jen povolená práva).
    ///   ScopeLevel=GLOBAL nebo ScopeMode=ALL → GlobalPermissions.
    ///   ScopeMode=INCLUDE + ProjectIds → PerProjectPermissions[projektId].
    /// </summary>
    private static AuthorizationSnapshot BuildSnapshot(bool isSuperAdmin, PermissionGrantViewModel[] grants)
    {
        var allowed = grants.Where(g => g.IsAllowed).ToList();

        var globalKeys = allowed
            .Where(g =>
                string.Equals(g.ScopeLevel, "GLOBAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(g.ScopeMode, "ALL", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.PermissionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var perProject = allowed
            .Where(g =>
                string.Equals(g.ScopeMode, "INCLUDE", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(g.ScopeLevel, "GLOBAL", StringComparison.OrdinalIgnoreCase))
            .SelectMany(g => g.ProjectIds.Select(pid => (pid, g.PermissionKey)))
            .GroupBy(x => x.pid)
            .ToDictionary(
                grp => grp.Key,
                grp => (IReadOnlySet<string>)new HashSet<string>(grp.Select(x => x.PermissionKey), StringComparer.OrdinalIgnoreCase));

        return new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: globalKeys,
            PerProjectPermissions: perProject,
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>());
    }

    private static CurrentUserContextViewModel BuildUser(
        bool isSuperAdmin,
        IReadOnlyList<int>? visibleProjectIds = null,
        IReadOnlyList<int>? deletedProjectIds = null,
        params PermissionGrantViewModel[] grants)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Test",
            Prijmeni = "User",
            DisplayName = "Test User",
            Email = "test.user@pmtracker.local",
            OrganizacniCelek = "Test",
            OrganizacniCelekKod = "TEST",
            IsSuperAdmin = isSuperAdmin,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = visibleProjectIds ?? grants.SelectMany(x => x.ProjectIds).Distinct().ToArray(),
            DeletedProjectIds = deletedProjectIds ?? Array.Empty<int>(),
            Authorization = BuildSnapshot(isSuperAdmin, grants)
        };
    }
}
