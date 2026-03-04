using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class PermissionEvaluationServiceTests
{
    [Fact]
    public void HasPermission_ShouldDelegateToCurrentUserContext()
    {
        var sut = new PermissionEvaluationService();
        var user = BuildUser(new PermissionGrantViewModel
        {
            PermissionKey = PermissionKeys.TeamManage,
            ScopeLevel = "PROJECT",
            ScopeMode = "INCLUDE",
            IsAllowed = true,
            ProjectIds = new[] { 7 }
        });

        sut.HasPermission(user, PermissionKeys.TeamManage, 7).Should().BeTrue();
        sut.HasPermission(user, PermissionKeys.TeamManage, 8).Should().BeFalse();
    }

    private static CurrentUserContextViewModel BuildUser(params PermissionGrantViewModel[] grants)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = 10,
            Jmeno = "Test",
            Prijmeni = "User",
            DisplayName = "Test User",
            Email = "test.user@pmtracker.local",
            OrganizacniCelekKod = "TEST",
            OrganizacniCelek = "Test",
            IsSuperAdmin = false,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = grants.SelectMany(x => x.ProjectIds).Distinct().ToArray(),
            DeletedProjectIds = Array.Empty<int>(),
            PermissionGrants = grants
        };
    }
}
