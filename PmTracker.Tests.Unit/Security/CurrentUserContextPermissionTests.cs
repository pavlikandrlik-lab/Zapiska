using FluentAssertions;
using PmTracker.Web.Models.ViewModels;

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
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.TeamManage,
                ScopeLevel = "PROJECT",
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = new[] { 7 }
            });

        user.HasPermission(PermissionKeys.TeamManage).Should().BeFalse("INCLUDE bez konkrétního projektu se nepočítá");
        user.HasPermission(PermissionKeys.TeamManage, 7).Should().BeTrue();
        user.HasPermission(PermissionKeys.TeamManage, 8).Should().BeFalse();
    }

    [Fact]
    public void HasPermission_ShouldHonorAllScope_ForAnyProject()
    {
        var user = BuildUser(
            isSuperAdmin: false,
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

    private static CurrentUserContextViewModel BuildUser(bool isSuperAdmin, params PermissionGrantViewModel[] grants)
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
            PermissionGrants = grants
        };
    }
}
