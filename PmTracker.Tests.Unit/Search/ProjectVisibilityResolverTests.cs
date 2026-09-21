using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Spec 2026-09-17 §2.3. Vada V1: dnešní vyhledávání pre-filtruje jen podle
/// VisibleProjectIds, takže kdo má přístup přes ROLI a ne přes obsazení projektu,
/// nevidí nic. Uživatel navíc výslovně požadoval, aby nové chování „prázdná množina
/// = žádné výsledky" nerozbilo app-admina a superadmina.
/// </summary>
public sealed class ProjectVisibilityResolverTests
{
    private static CurrentUserContextViewModel User(
        bool isSuperAdmin = false,
        int[]? visibleProjectIds = null,
        string[]? globalPermissions = null,
        Dictionary<int, IReadOnlySet<string>>? perProject = null) => new()
    {
        OsobaId = 1,
        Jmeno = "Jan",
        Prijmeni = "Novák",
        DisplayName = "Jan Novák",
        Email = "jan.novak@example.cz",
        OrganizacniCelek = "MO",
        IsSuperAdmin = isSuperAdmin,
        RoleKody = Array.Empty<string>(),
        VisibleProjectIds = visibleProjectIds ?? Array.Empty<int>(),
        DeletedProjectIds = Array.Empty<int>(),
        Authorization = new AuthorizationSnapshot(
            IsSuperAdmin: isSuperAdmin,
            GlobalPermissions: new HashSet<string>(globalPermissions ?? Array.Empty<string>()),
            PerProjectPermissions: perProject ?? new Dictionary<int, IReadOnlySet<string>>(),
            PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
    };

    private readonly ProjectVisibilityResolver _resolver = new();

    [Fact]
    public void Superadmin_NemaOmezeni()
    {
        _resolver.Resolve(User(isSuperAdmin: true)).Should().BeNull(
            "null znamená bez omezení — superadmin nesmí spadnout do prázdné množiny");
    }

    [Fact]
    public void AppAdmin_SGlobalnimReadAll_NemaOmezeni()
    {
        // APP_ADMIN i READ_ALL drží globálně projects.read.all.
        var user = User(globalPermissions: [PermissionKeys.ProjectsReadAll]);

        _resolver.Resolve(user).Should().BeNull();
    }

    [Fact]
    public void ClenProjektu_VidiSveProjekty()
    {
        var user = User(visibleProjectIds: [10, 20]);

        _resolver.Resolve(user).Should().BeEquivalentTo([10, 20]);
    }

    [Fact]
    public void DrzitelProjektoveRole_BezObsazeni_VidiSvujProjekt()
    {
        // Tohle je vada V1 — dnes by takový uživatel nedostal nic.
        var user = User(perProject: new Dictionary<int, IReadOnlySet<string>>
        {
            [42] = new HashSet<string> { PermissionKeys.RecordsEdit }
        });

        _resolver.Resolve(user).Should().BeEquivalentTo([42]);
    }

    [Fact]
    public void Obsazeni_ASnapshot_SeSlucujiBezDuplicit()
    {
        var user = User(
            visibleProjectIds: [10, 20],
            perProject: new Dictionary<int, IReadOnlySet<string>>
            {
                [20] = new HashSet<string> { PermissionKeys.RecordsEdit },
                [30] = new HashSet<string> { PermissionKeys.RecordsEdit }
            });

        _resolver.Resolve(user).Should().BeEquivalentTo([10, 20, 30]);
    }

    [Fact]
    public void PerProjektovyKlicBezPravaCist_ProjektNepridava()
    {
        var user = User(perProject: new Dictionary<int, IReadOnlySet<string>>
        {
            [42] = new HashSet<string> { "search.index" }
        });

        _resolver.Resolve(user).Should().BeEmpty(
            "search.index není project-read klíč, sám o sobě projekt nezpřístupní");
    }

    [Fact]
    public void UzivatelBezProjektu_VraciPrazdnoNeNull()
    {
        // Rozdíl, na kterém záleží: prázdná množina = žádné výsledky.
        // null by znamenalo „bez omezení", tedy únik všech záznamů.
        var result = _resolver.Resolve(User());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_JeVernymZrcadlem_CanAccessProject()
    {
        // Nejdůležitější invariant celé fičury: WHERE projekt_id IN Resolve(user)
        // musí vracet přesně ty záznamy, u kterých by CanAccessProject vrátilo true.
        // Kdyby se resolver a CanAccessProject rozešly, hledání by zpřístupnilo nebo
        // skrylo záznamy jinak než zbytek aplikace. Ověřujeme přes všechny tvary
        // uživatele a rozsah id projektů.
        var uzivatele = new[]
        {
            User(isSuperAdmin: true),
            User(globalPermissions: [PermissionKeys.ProjectsReadAll]),
            User(visibleProjectIds: [2, 5]),
            User(perProject: new Dictionary<int, IReadOnlySet<string>>
            {
                [3] = new HashSet<string> { PermissionKeys.RecordsEdit }, // projektová role bez obsazení
                [7] = new HashSet<string> { "search.index" }              // klíč, který ke čtení neopravňuje
            }),
            User(
                visibleProjectIds: [2],
                perProject: new Dictionary<int, IReadOnlySet<string>>
                {
                    [4] = new HashSet<string> { PermissionKeys.RecordsEdit }
                }),
            User() // nikam nepatří
        };

        foreach (var user in uzivatele)
        {
            var povolene = _resolver.Resolve(user);

            for (var projektId = 1; projektId <= 10; projektId++)
            {
                var hledaniPovoli = povolene is null || povolene.Contains(projektId);
                hledaniPovoli.Should().Be(user.CanAccessProject(projektId),
                    $"projekt {projektId} má být ve výsledcích právě tehdy, když ho CanAccessProject pustí");
            }
        }
    }

    [Fact]
    public void ChybejiciSnapshot_NevraciNull()
    {
        var user = new CurrentUserContextViewModel
        {
            OsobaId = 1,
            Jmeno = "Jan",
            Prijmeni = "Novák",
            DisplayName = "Jan Novák",
            Email = "jan.novak@example.cz",
            OrganizacniCelek = "MO",
            IsSuperAdmin = false,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = Array.Empty<int>(),
            DeletedProjectIds = Array.Empty<int>(),
            Authorization = null
        };

        _resolver.Resolve(user).Should().BeEmpty("bez snapshotu se nesmí povolit všechno");
    }
}
