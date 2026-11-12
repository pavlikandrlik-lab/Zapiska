using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PmTracker.Web.Data;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services;
using PmTracker.Web.Services.Audit;
using PmTracker.Web.Services.Common;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Services;

/// <summary>
/// Spec 2026-11-10 R6: příznaky „smí upravit / smí smazat“ pro ikony u vyjádření počítá
/// CommentService.GetCommentActionsAsync týmž pravidlem jako uložení. Uzavřené jednání = nic;
/// vyjádření bez jednání (orphan) smí smazat jen SuperAdmin, stejně jako DeleteCommentAsync.
/// </summary>
public sealed class CommentActionsTests
{
    private const int Projekt = 1;
    private const int Zaznam = 10;
    private const int JinyZaznam = 11;
    private const int DraftJednani = 20;
    private const int ClosedJednani = 21;

    private static PmTrackerDbContext CreateDb()
    {
        var db = new PmTrackerDbContext(new DbContextOptionsBuilder<PmTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CiselnikStavuJednani.AddRange(
            new CiselnikStavuJednaniEntity { Id = 1, Kod = "DRAFT", Nazev = "Příprava" },
            new CiselnikStavuJednaniEntity { Id = 3, Kod = "CLOSED", Nazev = "Uzavřeno" });
        db.ProjektoveZaznamy.AddRange(
            new ProjektovyZaznamEntity { Id = Zaznam, ProjektId = Projekt, SubsystemId = 100, Nazev = "Z" },
            new ProjektovyZaznamEntity { Id = JinyZaznam, ProjektId = Projekt, SubsystemId = 100, Nazev = "J" });
        db.Jednani.AddRange(
            new JednaniEntity { Id = DraftJednani, ProjektId = Projekt, CisloJednani = 1, StavJednaniId = 1 },
            new JednaniEntity { Id = ClosedJednani, ProjektId = Projekt, CisloJednani = 2, StavJednaniId = 3 });
        db.SaveChanges();
        return db;
    }

    private static int AddComment(PmTrackerDbContext db, int zaznamId, int jednaniId, int autor)
    {
        var comment = new VyjadreniEntity { ZaznamId = zaznamId, JednaniId = jednaniId, AutorOsobaId = autor, TextVyjadreni = "t", DatumVyjadreni = DateTime.Today };
        db.Vyjadreni.Add(comment);
        db.SaveChanges();
        return comment.Id;
    }

    private static CommentService CreateSut(PmTrackerDbContext db) => new(
        db, Mock.Of<IRichTextContentService>(), new CommentAuthorizationPolicy(), Mock.Of<IAuditWriteService>(),
        new ProjectRoleCache(db), TimeProvider.System);

    private static CurrentUserContextViewModel User(int osobaId, bool superAdmin, params string[] directKeys) => new()
    {
        OsobaId = osobaId,
        Jmeno = "T",
        Prijmeni = "U",
        DisplayName = "T U",
        Email = "t@u",
        OrganizacniCelekKod = "T",
        OrganizacniCelek = "T",
        IsSuperAdmin = superAdmin,
        RoleKody = Array.Empty<string>(),
        VisibleProjectIds = [Projekt],
        DeletedProjectIds = Array.Empty<int>(),
        Authorization = new AuthorizationSnapshot(
            IsSuperAdmin: superAdmin,
            GlobalPermissions: new HashSet<string>(),
            PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>> { { Projekt, directKeys.ToHashSet() } },
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>(),
            PerProjectDirectPermissions: new Dictionary<int, IReadOnlySet<string>> { { Projekt, directKeys.ToHashSet() } })
    };

    private static readonly string[] PmKeys =
    [
        PermissionKeys.CommentsAdd, PermissionKeys.CommentsEditOwn, PermissionKeys.CommentsEditAny,
        PermissionKeys.CommentsDeleteOwn, PermissionKeys.CommentsDeleteAny
    ];

    [Fact]
    public async Task Priznaky_respektuji_oddelene_klice()
    {
        await using var db = CreateDb();
        var id = AddComment(db, Zaznam, DraftJednani, autor: 6);
        var gestor = User(6, false, PermissionKeys.CommentsAdd, PermissionKeys.CommentsEditOwn);

        var actions = await CreateSut(db).GetCommentActionsAsync(Zaznam, [id], gestor);

        actions[id].Should().Be(new CommentActions(CanEdit: true, CanDelete: false));
    }

    [Fact]
    public async Task Orphan_vyjadreni_smaze_jen_superadmin()
    {
        await using var db = CreateDb();
        var id = AddComment(db, Zaznam, jednaniId: 999, autor: 5);
        var sut = CreateSut(db);

        (await sut.GetCommentActionsAsync(Zaznam, [id], User(1, true)))[id].Should().Be(new CommentActions(false, true));
        (await sut.GetCommentActionsAsync(Zaznam, [id], User(5, false, PmKeys)))[id].Should().Be(new CommentActions(false, false));
    }

    [Fact]
    public async Task Uzavrene_jednani_nic()
    {
        await using var db = CreateDb();
        var id = AddComment(db, Zaznam, ClosedJednani, autor: 5);

        var actions = await CreateSut(db).GetCommentActionsAsync(Zaznam, [id], User(5, false, PmKeys));

        actions[id].Should().Be(new CommentActions(false, false));
    }

    [Fact]
    public async Task Cizi_id_ve_vysledku_chybi()
    {
        await using var db = CreateDb();
        var vlastni = AddComment(db, Zaznam, DraftJednani, autor: 5);
        var jineho = AddComment(db, JinyZaznam, DraftJednani, autor: 5);

        var actions = await CreateSut(db).GetCommentActionsAsync(Zaznam, [vlastni, jineho], User(5, false, PmKeys));

        actions.Keys.Should().Equal(vlastni);
        actions[vlastni].Should().Be(new CommentActions(true, true));
    }
}
