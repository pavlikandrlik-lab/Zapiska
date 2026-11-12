using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Common;

public sealed class CommentAuthorizationPolicyTests
{
    private readonly CommentAuthorizationPolicy _sut = new();

    // -----------------------------------------------------------------------
    // Spec 2026-11-10 (vyjádření podle role): přidání, úprava a smazání
    // -----------------------------------------------------------------------

    private const int Projekt = 2;
    private const int CiziAutor = 777;

    private static readonly IReadOnlySet<string> PlneKlice = new HashSet<string>
    {
        PermissionKeys.CommentsAdd, PermissionKeys.CommentsEditOwn, PermissionKeys.CommentsEditAny,
        PermissionKeys.CommentsDeleteOwn, PermissionKeys.CommentsDeleteAny
    };

    private static readonly IReadOnlySet<string> VlastniKlice = new HashSet<string>
    {
        PermissionKeys.CommentsAdd, PermissionKeys.CommentsEditOwn, PermissionKeys.CommentsDeleteOwn
    };

    /// <summary>Uživatel podle profilu role; subsystémové role mají klíče jen zděděné (efektivní, ne přímé).</summary>
    private static (CurrentUserContextViewModel User, bool JeVedouci) Profil(string profil) => profil switch
    {
        "PM" => (Primy(5, PlneKlice), false),
        "Gestor" => (Primy(6, VlastniKlice), false),
        "Vedouci" => (Zdedeny(12, VlastniKlice.Append(PermissionKeys.MeetingsNotesSubsystemLead)), true),
        "Metodik" => (Zdedeny(13, VlastniKlice), false),
        "Host" => (Primy(14, new HashSet<string>()), false),
        _ => throw new ArgumentOutOfRangeException(nameof(profil))
    };

    private static CurrentUserContextViewModel Primy(int osobaId, IEnumerable<string> klice) => BuildUser(
        osobaId,
        visibleProjectIds: [Projekt],
        perProjectPermissions: new Dictionary<int, IReadOnlySet<string>> { { Projekt, klice.ToHashSet() } },
        perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>> { { Projekt, klice.ToHashSet() } });

    private static CurrentUserContextViewModel Zdedeny(int osobaId, IEnumerable<string> klice) => BuildUser(
        osobaId,
        visibleProjectIds: [Projekt],
        perProjectPermissions: new Dictionary<int, IReadOnlySet<string>> { { Projekt, klice.ToHashSet() } },
        perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>>());

    /// <summary>Scope záznamu: osoba je v seznamech subsystému záznamu jen u vlastního subsystému.</summary>
    private static CommentScope Scope(int osobaId, bool priprava, bool vlastniSubsystem, bool jeVedouci) => new(
        Projekt,
        priprava,
        SubsystemRoleOsobaIds: vlastniSubsystem ? [osobaId, 99] : [99],
        SubsystemLeadEquivalentOsobaIds: vlastniSubsystem && jeVedouci ? [osobaId] : [99]);

    [Theory]
    //           profil     příprava vlastníSub  přidat  upravitVl smazatVl upravitCizí smazatCizí
    [InlineData("PM",       true,    true,       true,   true,     true,    true,       true)]
    [InlineData("PM",       false,   false,      true,   true,     true,    true,       true)]
    [InlineData("Gestor",   true,    false,      true,   true,     true,    false,      false)]
    [InlineData("Gestor",   false,   false,      true,   true,     true,    false,      false)]
    [InlineData("Vedouci",  true,    true,       true,   true,     true,    false,      false)]
    [InlineData("Vedouci",  true,    false,      false,  false,    false,   false,      false)]
    [InlineData("Vedouci",  false,   true,       false,  false,    false,   false,      false)]
    [InlineData("Metodik",  true,    true,       true,   true,     true,    false,      false)]
    [InlineData("Metodik",  true,    false,      false,  false,    false,   false,      false)]
    [InlineData("Metodik",  false,   true,       false,  false,    false,   false,      false)]
    [InlineData("Host",     true,    true,       false,  false,    false,   false,      false)]
    public void Matice_R1(string profil, bool priprava, bool vlastniSubsystem,
        bool pridat, bool upravitVlastni, bool smazatVlastni, bool upravitCizi, bool smazatCizi)
    {
        var (user, jeVedouci) = Profil(profil);
        var scope = Scope(user.OsobaId, priprava, vlastniSubsystem, jeVedouci);

        _sut.CanAddComment(user, scope).Should().Be(pridat, "přidat");
        _sut.CanEditComment(user, scope, user.OsobaId).Should().Be(upravitVlastni, "upravit vlastní");
        _sut.CanDeleteComment(user, scope, user.OsobaId).Should().Be(smazatVlastni, "smazat vlastní");
        _sut.CanEditComment(user, scope, CiziAutor).Should().Be(upravitCizi, "upravit cizí");
        _sut.CanDeleteComment(user, scope, CiziAutor).Should().Be(smazatCizi, "smazat cizí");
    }

    [Fact]
    public void Vedouci_jen_s_klicem_zapisu_ma_vyjimku_jen_v_priprave()
    {
        // Rozhodnutí 2026-06-16: vedoucí / zástupce s meetings.notes.subsystemlead i bez klíčů comments.*.
        var user = Zdedeny(12, [PermissionKeys.MeetingsNotesSubsystemLead]);
        var priprava = new CommentScope(Projekt, true, [12], [12]);
        var otevreno = new CommentScope(Projekt, false, [12], [12]);

        _sut.CanAddComment(user, priprava).Should().BeTrue();
        _sut.CanEditComment(user, priprava, 12).Should().BeTrue();
        _sut.CanDeleteComment(user, priprava, 12).Should().BeTrue();
        _sut.CanAddComment(user, otevreno).Should().BeFalse();
        _sut.CanEditComment(user, otevreno, 12).Should().BeFalse();
        _sut.CanDeleteComment(user, otevreno, 12).Should().BeFalse();
    }

    [Fact]
    public void Gestor_ktery_je_i_metodikem_se_ridi_gestorem()
    {
        // R4 „vyšší bere“: přímá role Gestora, osoba je zároveň metodikem jiného subsystému.
        var user = Primy(6, VlastniKlice);
        var ciziSubsystemOtevreno = new CommentScope(Projekt, false, [99], [99]);

        _sut.CanAddComment(user, ciziSubsystemOtevreno).Should().BeTrue();
        _sut.CanEditComment(user, ciziSubsystemOtevreno, 6).Should().BeTrue();
    }

    [Fact]
    public void Metodik_ve_dvou_subsystemech_smi_v_obou()
    {
        var user = Zdedeny(13, VlastniKlice);

        _sut.CanAddComment(user, new CommentScope(Projekt, true, [13], [])).Should().BeTrue();
        _sut.CanAddComment(user, new CommentScope(Projekt, true, [13, 40], [])).Should().BeTrue();
        _sut.CanAddComment(user, new CommentScope(Projekt, true, [40], [])).Should().BeFalse();
    }

    [Fact]
    public void Metodik_bez_role_v_subsystemu_nesmi_nic()
    {
        // Zděděné klíče ve snapshotu, ale osoba v žádném seznamu subsystému (role odebrána).
        var user = Zdedeny(13, VlastniKlice);
        var scope = new CommentScope(Projekt, true, [], []);

        _sut.CanAddComment(user, scope).Should().BeFalse();
        _sut.CanEditComment(user, scope, 13).Should().BeFalse();
        _sut.CanDeleteComment(user, scope, 13).Should().BeFalse();
    }

    [Fact]
    public void Uprava_a_smazani_se_ridi_vlastnimi_klici()
    {
        // R5: úprava podle comments.edit.*, smazání podle comments.delete.*.
        var gestorBezMazani = Primy(6, [PermissionKeys.CommentsAdd, PermissionKeys.CommentsEditOwn]);
        var scope = new CommentScope(Projekt, true, [], []);
        _sut.CanEditComment(gestorBezMazani, scope, 6).Should().BeTrue();
        _sut.CanDeleteComment(gestorBezMazani, scope, 6).Should().BeFalse();

        var pmBezMazaniCizich = Primy(5, [PermissionKeys.CommentsAdd, PermissionKeys.CommentsEditAny]);
        _sut.CanEditComment(pmBezMazaniCizich, scope, CiziAutor).Should().BeTrue();
        _sut.CanDeleteComment(pmBezMazaniCizich, scope, CiziAutor).Should().BeFalse();
    }

    [Fact]
    public void Smazany_projekt_nepovoli_pridat()
    {
        var user = BuildUser(6, visibleProjectIds: [Projekt], deletedProjectIds: [Projekt],
            perProjectPermissions: new Dictionary<int, IReadOnlySet<string>> { { Projekt, VlastniKlice } },
            perProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>> { { Projekt, VlastniKlice } });

        _sut.CanAddComment(user, new CommentScope(Projekt, true, [], [])).Should().BeFalse();
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
        _sut.CanAddComment(user, new CommentScope(2, IsDraftMeeting: true, SubsystemRoleOsobaIds: [12], SubsystemLeadEquivalentOsobaIds: [12])).Should().BeFalse();
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
