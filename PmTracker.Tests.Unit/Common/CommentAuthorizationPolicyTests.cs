using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;

namespace PmTracker.Tests.Unit.Common;

public sealed class CommentAuthorizationPolicyTests
{
    private readonly CommentAuthorizationPolicy _sut = new(new PermissionEvaluationService());

    [Fact]
    public void CanAddComment_ShouldAllow_WhenUserHasRecordsEdit()
    {
        var user = BuildUser(
            osobaId: 5,
            new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsEdit,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        _sut.CanAddComment(user, projektId: 2, subsystemLeadOsobaId: 999).Should().BeTrue();
    }

    [Fact]
    public void CanCommentAsSubsystemLeader_ShouldAllowOnlyMatchingLeaderWithPermission()
    {
        var user = BuildUser(
            osobaId: 12,
            new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsCommentSubsystemLead,
                ScopeLevel = "PROJECT",
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = new[] { 2 }
            });

        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadOsobaId: 12).Should().BeTrue();
        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadOsobaId: 13).Should().BeFalse();
        _sut.CanCommentAsSubsystemLeader(user, projektId: 3, subsystemLeadOsobaId: 12).Should().BeFalse();
    }

    [Fact]
    public void CanModifyComment_ShouldAllowOnlyOwnComment_InSubsystemLeadMode()
    {
        var user = BuildUser(
            osobaId: 20,
            new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsCommentSubsystemLead,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        _sut.CanModifyComment(user, projektId: 9, subsystemLeadOsobaId: 20, commentAuthorOsobaId: 20).Should().BeTrue();
        _sut.CanModifyComment(user, projektId: 9, subsystemLeadOsobaId: 20, commentAuthorOsobaId: 30).Should().BeFalse();
    }

    private static CurrentUserContextViewModel BuildUser(int osobaId, params PermissionGrantViewModel[] grants)
    {
        return new CurrentUserContextViewModel
        {
            OsobaId = osobaId,
            Jmeno = "Test",
            Prijmeni = "User",
            DisplayName = "Test User",
            Email = "test.user@pmtracker.local",
            OrganizacniCelekKod = "TEST",
            OrganizacniCelek = "Test",
            IsSuperAdmin = false,
            RoleKody = Array.Empty<string>(),
            PermissionGrants = grants
        };
    }
}
