using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;

namespace PmTracker.Tests.Unit.Services.ServiceDesk;

public sealed class SdInfoSystemyTests
{
    [Fact]
    public void Vychozi_Obsahuje2Polozky_FisAIssp()
    {
        SdInfoSystemy.Vychozi.Should().HaveCount(2);
        SdInfoSystemy.Vychozi.Select(x => x.Zkratka).Should().Equal("FIS", "ISSP");
    }

    [Fact]
    public void Vychozi_SerazenoPodleId()
    {
        var ids = SdInfoSystemy.Vychozi.Select(x => x.Id).ToList();
        ids.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ById_ExistujiciId_VraciZaznam()
    {
        var fis = SdInfoSystemy.ById(SdInfoSystemy.FisId);
        fis.Should().NotBeNull();
        fis!.Zkratka.Should().Be("FIS");
        fis.Nazev.Should().Contain("Finanční");
    }

    [Fact]
    public void ById_NeznameId_VraciNull()
    {
        SdInfoSystemy.ById(999).Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(1, true)]   // FIS
    [InlineData(2, true)]   // ISSP
    [InlineData(3, false)]  // X_FIS — úmyslně out of scope
    [InlineData(999, false)]
    public void IsSupported_VraciSpravneHodnoty(int? id, bool expected)
    {
        SdInfoSystemy.IsSupported(id).Should().Be(expected);
    }

    [Fact]
    public void Konstanty_OdpovidajiVychozimuKatalogu()
    {
        SdInfoSystemy.Vychozi.Should().Contain(x => x.Id == SdInfoSystemy.FisId && x.Zkratka == "FIS");
        SdInfoSystemy.Vychozi.Should().Contain(x => x.Id == SdInfoSystemy.IsspId && x.Zkratka == "ISSP");
    }
}
