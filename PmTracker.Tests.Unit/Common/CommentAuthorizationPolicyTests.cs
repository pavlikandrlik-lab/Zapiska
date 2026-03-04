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
            visibleProjectIds: [2],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsEdit,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [999]).Should().BeTrue();
    }

    [Fact]
    public void CanCommentAsSubsystemLeader_ShouldAllowMatchingLeaderOrDeputyWithPermission()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsCommentSubsystemLead,
                ScopeLevel = "PROJECT",
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = new[] { 2 }
            });

        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12, 14]).Should().BeTrue();
        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [13, 14]).Should().BeFalse();
        _sut.CanCommentAsSubsystemLeader(user, projektId: 3, subsystemLeadEquivalentOsobaIds: [12, 14]).Should().BeFalse();
    }

    [Fact]
    public void CanModifyComment_ShouldAllowOnlyOwnComment_InSubsystemLeadMode()
    {
        var user = BuildUser(
            osobaId: 20,
            visibleProjectIds: [9],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsCommentSubsystemLead,
                ScopeLevel = "PROJECT",
                ScopeMode = "ALL",
                IsAllowed = true,
                ProjectIds = Array.Empty<int>()
            });

        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [20, 21], commentAuthorOsobaId: 20).Should().BeTrue();
        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [20, 21], commentAuthorOsobaId: 30).Should().BeFalse();
    }

    [Fact]
    public void CanCommentAsSubsystemLeader_ShouldReturnFalse_ForDeletedProject()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            deletedProjectIds: [2],
            grants: new PermissionGrantViewModel
            {
                PermissionKey = PermissionKeys.RecordsCommentSubsystemLead,
                ScopeLevel = "PROJECT",
                ScopeMode = "INCLUDE",
                IsAllowed = true,
                ProjectIds = new[] { 2 }
            });

        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12]).Should().BeFalse();
        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12]).Should().BeFalse();
    }

    private static CurrentUserContextViewModel BuildUser(
        int osobaId,
        IReadOnlyList<int>? visibleProjectIds = null,
        IReadOnlyList<int>? deletedProjectIds = null,
        params PermissionGrantViewModel[] grants)
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
            VisibleProjectIds = visibleProjectIds ?? grants.SelectMany(x => x.ProjectIds).Distinct().ToArray(),
            DeletedProjectIds = deletedProjectIds ?? Array.Empty<int>(),
            PermissionGrants = grants
        };
    }
}
