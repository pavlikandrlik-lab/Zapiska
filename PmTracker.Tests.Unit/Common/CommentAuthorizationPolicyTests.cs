using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Common;

public sealed class CommentAuthorizationPolicyTests
{
    private readonly CommentAuthorizationPolicy _sut = new();

    [Fact]
    public void CanAddComment_ShouldAllow_WhenUserHasRecordsEdit()
    {
        var user = BuildUser(
            osobaId: 5,
            visibleProjectIds: [2],
            globalPermissions: [PermissionKeys.RecordsEdit]);

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [999], isDraftMeeting: false).Should().BeTrue();
    }

    [Fact]
    public void CanCommentAsSubsystemLeader_ShouldAllowMatchingLeaderOrDeputyWithPermission()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.RecordsCommentSubsystemLead } }
            });

        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12, 14]).Should().BeTrue();
        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [13, 14]).Should().BeFalse();
        _sut.CanCommentAsSubsystemLeader(user, projektId: 3, subsystemLeadEquivalentOsobaIds: [12, 14]).Should().BeFalse();
    }

    [Fact]
    public void CanModifyComment_ShouldAllowOnlyOwnComment_InSubsystemLeadModeForDraft()
    {
        var user = BuildUser(
            osobaId: 20,
            visibleProjectIds: [9],
            globalPermissions: [PermissionKeys.RecordsCommentSubsystemLead]);

        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [20, 21], commentAuthorOsobaId: 20, isDraftMeeting: true).Should().BeTrue();
        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [20, 21], commentAuthorOsobaId: 30, isDraftMeeting: true).Should().BeFalse();
    }

    [Fact]
    public void CanAddComment_ShouldDenySubsystemLeaderSpecialRight_InOpen()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.RecordsCommentSubsystemLead } }
            });

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12], isDraftMeeting: false).Should().BeFalse();
    }

    [Fact]
    public void CanModifyComment_ShouldDenySubsystemLeaderSpecialRight_InOpen()
    {
        var user = BuildUser(
            osobaId: 20,
            visibleProjectIds: [9],
            globalPermissions: [PermissionKeys.RecordsCommentSubsystemLead]);

        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [20, 21], commentAuthorOsobaId: 20, isDraftMeeting: false).Should().BeFalse();
    }

    [Fact]
    public void CanModifyComment_ShouldAllowForeignComment_WhenUserHasRecordsEdit()
    {
        var user = BuildUser(
            osobaId: 20,
            visibleProjectIds: [9],
            globalPermissions: [PermissionKeys.RecordsEdit]);

        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [21], commentAuthorOsobaId: 30, isDraftMeeting: false).Should().BeTrue();
    }

    [Fact]
    public void CanCommentAsSubsystemLeader_ShouldReturnFalse_ForDeletedProject()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            deletedProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.RecordsCommentSubsystemLead } }
            });

        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12]).Should().BeFalse();
        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12], isDraftMeeting: true).Should().BeFalse();
    }

    private static CurrentUserContextViewModel BuildUser(
        int osobaId,
        IReadOnlyList<int>? visibleProjectIds = null,
        IReadOnlyList<int>? deletedProjectIds = null,
        IReadOnlyCollection<string>? globalPermissions = null,
        IReadOnlyDictionary<int, IReadOnlySet<string>>? perProjectPermissions = null)
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
            VisibleProjectIds = visibleProjectIds ?? Array.Empty<int>(),
            DeletedProjectIds = deletedProjectIds ?? Array.Empty<int>(),
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: false,
                GlobalPermissions: new HashSet<string>(globalPermissions ?? []),
                PerProjectPermissions: perProjectPermissions ?? new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
    }
}
