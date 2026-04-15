using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Search;

namespace PmTracker.Tests.Unit.Search;

public sealed class EntityDocumentMapperTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MapProjekt_PreferujeCelyNazev_A_ZkratkuDaDoKeywords()
    {
        var p = new ProjektEntity { Id = 3, Zkratka = "ABC", CelyNazev = "Alfa Beta Centauri" };
        var doc = EntityDocumentMapper.MapProjekt(p, Now);
        doc.EntityType.Should().Be("projekt");
        doc.EntityId.Should().Be("3");
        doc.ProjektId.Should().Be(3);
        doc.Title.Should().Be("Alfa Beta Centauri");
        doc.Keywords.Should().Contain("ABC");
    }

    [Fact]
    public void MapProjekt_FallbackNaZkratku_KdyzCelyNazevPrazdny()
    {
        var p = new ProjektEntity { Id = 1, Zkratka = "X", CelyNazev = "" };
        EntityDocumentMapper.MapProjekt(p, Now).Title.Should().Be("X");
    }

    [Fact]
    public void MapZaznam_SpojiCilAPopisDoBody()
    {
        var z = new ProjektovyZaznamEntity
        {
            Id = 7, ProjektId = 42, Nazev = "Test záznam",
            Cil = "Cíl textu", Popis = "Popis detailu", CisloViditelne = "Z-7", CisloZaznamu = 7, SubsystemId = 2
        };
        var doc = EntityDocumentMapper.MapZaznam(z, Now);
        doc.Title.Should().Be("Test záznam");
        doc.Body.Should().Contain("Cíl").And.Contain("Popis");
        doc.Keywords.Should().Contain("Z-7");
        doc.ProjektId.Should().Be(42);
    }

    [Fact]
    public void MapOsoba_PouzijeFullName_A_AdLoginDoKeywords()
    {
        var o = new OsobaEntity { Id = 5, Jmeno = "Jan", Prijmeni = "Novák", Titul = "Ing.", Email = "jan@x", AdLogin = "jnovak" };
        var doc = EntityDocumentMapper.MapOsoba(o, Now);
        doc.Title.Should().Contain("Jan").And.Contain("Novák");
        doc.Keywords.Should().Contain("jnovak").And.Contain("jan@x");
        doc.ProjektId.Should().BeNull();
    }

    [Fact]
    public void MapVyjadreni_PouzijeDodanyProjektId()
    {
        var v = new VyjadreniEntity { Id = 9, ZaznamId = 4, JednaniId = 8, TextVyjadreni = "Obsah komentáře" };
        var doc = EntityDocumentMapper.MapVyjadreni(v, projektId: 55, Now);
        doc.ProjektId.Should().Be(55);
        doc.Body.Should().Be("Obsah komentáře");
        doc.Meta["zaznam_id"].Should().Be("4");
    }

    [Fact]
    public void MapSubsystem_NemaProjektId()
    {
        var s = new SubsystemEntity { Id = 2, Kod = "SUB", Nazev = "Subsystém X" };
        var doc = EntityDocumentMapper.MapSubsystem(s, Now);
        doc.ProjektId.Should().BeNull();
        doc.Keywords.Should().Contain("SUB");
    }
}
