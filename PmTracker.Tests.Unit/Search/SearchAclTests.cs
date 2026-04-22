using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Search;
using PmTracker.Web.Services.Security;

namespace PmTracker.Tests.Unit.Search;

public sealed class SearchAclTests
{
    [Fact]
    public void SuperAdmin_SeesEverything()
    {
        var user = BuildUser(isSuperAdmin: true);
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeProjekt, projektId: 99)).Should().BeTrue();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeZaznam, projektId: 99)).Should().BeTrue();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeVyjadreni, projektId: null)).Should().BeTrue();
    }

    [Fact]
    public void Osoba_A_Subsystem_JsouVzdyVidet_ProBezneUzivatele()
    {
        var user = BuildUser(isSuperAdmin: false);
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeOsoba)).Should().BeTrue();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeSubsystem)).Should().BeTrue();
    }

    [Fact]
    public void Projekt_Scope_RespektujeVisibleProjectIds()
    {
        var user = BuildUser(isSuperAdmin: false, visibleProjectIds: new[] { 10 });
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeProjekt, projektId: 10)).Should().BeTrue();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeProjekt, projektId: 11)).Should().BeFalse();
    }

    [Fact]
    public void Zaznam_Vyjadreni_ZaznamNavrh_RespektujiScope()
    {
        var user = BuildUser(isSuperAdmin: false, visibleProjectIds: new[] { 5 });
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeZaznam, projektId: 5)).Should().BeTrue();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeJednani, projektId: 5)).Should().BeTrue();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeVyjadreni, projektId: 5)).Should().BeTrue();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeZaznamNavrh, projektId: 5)).Should().BeTrue();

        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeZaznam, projektId: 6)).Should().BeFalse();
        SearchAcl.IsAccessible(user, Hit(EntityDocumentMapper.TypeVyjadreni, projektId: null)).Should().BeFalse();
    }

    [Fact]
    public void NeznamyTyp_JeOdmitnut()
    {
        var user = BuildUser(isSuperAdmin: false);
        SearchAcl.IsAccessible(user, Hit("neco_jineho", projektId: 1)).Should().BeFalse();
    }

    private static SearchHit Hit(string type, int? projektId = null) => new()
    {
        EntityType = type,
        EntityId = "1",
        ProjektId = projektId,
        Title = "t"
    };

    private static CurrentUserContextViewModel BuildUser(bool isSuperAdmin, IReadOnlyList<int>? visibleProjectIds = null)
        => new()
        {
            OsobaId = 1,
            Jmeno = "T",
            Prijmeni = "U",
            DisplayName = "T U",
            Email = "t@u",
            OrganizacniCelek = "x",
            OrganizacniCelekKod = "X",
            IsSuperAdmin = isSuperAdmin,
            RoleKody = Array.Empty<string>(),
            VisibleProjectIds = visibleProjectIds ?? Array.Empty<int>(),
            DeletedProjectIds = Array.Empty<int>(),
            Authorization = new AuthorizationSnapshot(
                IsSuperAdmin: isSuperAdmin,
                GlobalPermissions: new HashSet<string>(),
                PerProjectPermissions: new Dictionary<int, IReadOnlySet<string>>(),
                PerSubsystemPermissions: new Dictionary<int, IReadOnlySet<string>>())
        };
}
