using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Common;

public sealed class CommentAuthorizationPolicyTests
{
    private readonly CommentAuthorizationPolicy _sut = new();

    [Fact]
    public void CanAddComment_ShouldAllow_WhenUserHasCommentsAdd()
    {
        var user = BuildUser(
            osobaId: 5,
            visibleProjectIds: [2],
            globalPermissions: [PermissionKeys.CommentsAdd]);

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [999], isSubsystemLeadEquivalentInProject: false, isDraftMeeting: false).Should().BeTrue();
    }

    [Fact]
    public void CanAddComment_ShouldAllow_ProjectRole_AnywhereInProject_EvenInOpenMeeting()
    {
        // Přímá projektová role (comments.add v perProjectDirect) → komentuje kdekoli, i v OPEN.
        var user = BuildUser(
            osobaId: 5,
            visibleProjectIds: [2],
            perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.CommentsAdd } }
            });

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [999], isSubsystemLeadEquivalentInProject: false, isDraftMeeting: false).Should().BeTrue();
    }

    [Fact]
    public void CanAddComment_ShouldAllow_DualRole_ProjectRolePlusSubsystemLead_AnywhereInProject()
    {
        // „Vyšší bere": kdo má comments.add z projektové role, není omezen subsystémem,
        // i když je zároveň vedoucím nějakého subsystému (isSubsystemLeadEquivalentInProject=true).
        var user = BuildUser(
            osobaId: 5,
            visibleProjectIds: [2],
            perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.CommentsAdd } }
            });

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [99], isSubsystemLeadEquivalentInProject: true, isDraftMeeting: false).Should().BeTrue();
    }

    [Fact]
    public void CanAddComment_ShouldDeny_SubsystemLead_ForForeignSubsystem()
    {
        // Vedoucí subsystému A (NEní v lead-equivalent listu záznamu = subsystém B) → nesmí, ani v DRAFT.
        // Subsystémový lead: comments.add je v EFEKTIVNÍ sadě (zděděno), ale NE v přímé (direct).
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead, PermissionKeys.CommentsAdd } }
            },
            perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>>());

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [99], isSubsystemLeadEquivalentInProject: true, isDraftMeeting: true).Should().BeFalse();
    }

    [Fact]
    public void CanAddComment_ShouldAllow_SubsystemLead_ForOwnSubsystem_InDraft()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead, PermissionKeys.CommentsAdd } }
            },
            perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>>());

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12, 14], isSubsystemLeadEquivalentInProject: true, isDraftMeeting: true).Should().BeTrue();
    }

    [Fact]
    public void CanAddComment_ShouldPreserve_NonLeadSubsystemRole_WithInheritedCommentsAdd()
    {
        // METODIK subsystému: má comments.add (zděděno do efektivní project sady, NE direct), ale NENÍ
        // lead-equivalent (isSubsystemLeadEquivalentInProject=false) → zachované chování (smí).
        var user = BuildUser(
            osobaId: 7,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.CommentsAdd } }
            },
            perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>>());

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [99], isSubsystemLeadEquivalentInProject: false, isDraftMeeting: false).Should().BeTrue();
    }

    [Fact]
    public void CanCommentAsSubsystemLeader_ShouldAllowMatchingLeaderOrDeputyWithPermission()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead } }
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
            globalPermissions: [PermissionKeys.MeetingsNotesSubsystemLead, PermissionKeys.CommentsEditOwn]);

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
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead } }
            });

        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12], isSubsystemLeadEquivalentInProject: true, isDraftMeeting: false).Should().BeFalse();
    }

    [Fact]
    public void CanModifyComment_ShouldDenySubsystemLeaderSpecialRight_InOpen()
    {
        var user = BuildUser(
            osobaId: 20,
            visibleProjectIds: [9],
            globalPermissions: [PermissionKeys.MeetingsNotesSubsystemLead]);

        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [20, 21], commentAuthorOsobaId: 20, isDraftMeeting: false).Should().BeFalse();
    }

    [Fact]
    public void CanModifyComment_ShouldAllowForeignComment_WhenUserHasCommentsEditAny()
    {
        var user = BuildUser(
            osobaId: 20,
            visibleProjectIds: [9],
            globalPermissions: [PermissionKeys.CommentsEditAny]);

        _sut.CanModifyComment(user, projektId: 9, subsystemLeadEquivalentOsobaIds: [21], commentAuthorOsobaId: 30, isDraftMeeting: false).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // CanSaveMeetingNote — F3.8 duální gate pro zápis jednání
    // -----------------------------------------------------------------------

    [Fact]
    public void CanSaveMeetingNote_ShouldAllow_WhenUserHasMeetingsNotesEdit_Regardless_OfSubsystem()
    {
        // meetings.notes.edit = admin / PM → píše zápis kterémukoliv subsystému, i mimo DRAFT.
        var user = BuildUser(
            osobaId: 5,
            visibleProjectIds: [2],
            globalPermissions: [PermissionKeys.MeetingsNotesEdit]);

        _sut.CanSaveMeetingNote(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [999], isDraftMeeting: false)
            .Should().BeTrue();
        _sut.CanSaveMeetingNote(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [999], isDraftMeeting: true)
            .Should().BeTrue();
    }

    [Fact]
    public void CanSaveMeetingNote_ShouldAllow_WhenUserHasSubsystemLead_AndIsLeadEquivalent_InDraft()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead } }
            });

        _sut.CanSaveMeetingNote(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12, 14], isDraftMeeting: true)
            .Should().BeTrue();
    }

    [Fact]
    public void CanSaveMeetingNote_ShouldDeny_SubsystemLead_ForForeignSubsystem()
    {
        // F3.8 service filter: vedoucí subsystému A NEsmí psát zápis pro subsystém B.
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead } }
            });

        // Subsystém, kde NENÍ v lead-equivalent listu.
        _sut.CanSaveMeetingNote(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [99], isDraftMeeting: true)
            .Should().BeFalse();
    }

    [Fact]
    public void CanSaveMeetingNote_ShouldDeny_SubsystemLead_OutsideDraft()
    {
        var user = BuildUser(
            osobaId: 12,
            visibleProjectIds: [2],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>>
            {
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead } }
            });

        _sut.CanSaveMeetingNote(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12], isDraftMeeting: false)
            .Should().BeFalse();
    }

    [Fact]
    public void CanSaveMeetingNote_ShouldDeny_GenericCommentsAdd()
    {
        // comments.add (obecný klíč pro vyjádření) NEDÁVÁ právo psát zápis jednání.
        var user = BuildUser(
            osobaId: 7,
            visibleProjectIds: [2],
            globalPermissions: [PermissionKeys.CommentsAdd]);

        _sut.CanSaveMeetingNote(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [7], isDraftMeeting: true)
            .Should().BeFalse();
    }

    [Fact]
    public void CanSaveMeetingNote_ShouldDeny_InDeletedProject()
    {
        var user = BuildUser(
            osobaId: 5,
            visibleProjectIds: [2],
            deletedProjectIds: [2],
            globalPermissions: [PermissionKeys.MeetingsNotesEdit]);

        _sut.CanSaveMeetingNote(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [], isDraftMeeting: true)
            .Should().BeFalse();
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
                { 2, new HashSet<string> { PermissionKeys.MeetingsNotesSubsystemLead } }
            });

        _sut.CanCommentAsSubsystemLeader(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12]).Should().BeFalse();
        _sut.CanAddComment(user, projektId: 2, subsystemLeadEquivalentOsobaIds: [12], isSubsystemLeadEquivalentInProject: true, isDraftMeeting: true).Should().BeFalse();
    }

    private static CurrentUserContextViewModel BuildUser(
        int osobaId,
        IReadOnlyList<int>? visibleProjectIds = null,
        IReadOnlyList<int>? deletedProjectIds = null,
        IReadOnlyCollection<string>? globalPermissions = null,
        IReadOnlyDictionary<int, IReadOnlySet<string>>? perProjectPermissions = null,
        IReadOnlyDictionary<int, IReadOnlySet<string>>? perProjectDirectPermissions = null)
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
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerProjectDirectPermissions: perProjectDirectPermissions)
        };
    }
}
