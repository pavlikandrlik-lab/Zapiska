using FluentAssertions;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

/// <summary>
/// HOT_KALKULACE.rozpad_licence je HTML tabulka „název | cena", kterou píše ServiceDesk
/// (spec 2026-09-10 B5). Vzorek níž je doslova z produkce (id 5414).
/// </summary>
public sealed class RozpadLicenceParserTests
{
    private const string VzorekZProdukce =
        "<table style='width: 100%;'  cellspacing='0' cellpadding='0'><tr><td>RZA Registr zakázek – rozšíření – evidenční přenos VZ do NEN</td><td align='right'>10340,00</td></tr></table>";

    [Fact]
    public void Parse_VzorekZProdukce_JedenRadek()
    {
        var polozka = RozpadLicenceParser.Parse(VzorekZProdukce).Should().ContainSingle().Which;

        polozka.Nazev.Should().Be("RZA Registr zakázek – rozšíření – evidenční přenos VZ do NEN");
        polozka.Cena.Should().Be(10340m);
    }

    [Fact]
    public void Parse_ViceRadku_ZachovaPoradi()
    {
        var html = "<table><tr><td>XRG – RSS Rozhraní</td><td>94 470,00</td></tr>"
                 + "<tr><td>XRG - ESS Rozhraní</td><td>94&nbsp;470,00</td></tr></table>";

        var polozky = RozpadLicenceParser.Parse(html);

        polozky.Select(p => p.Nazev).Should().Equal("XRG – RSS Rozhraní", "XRG - ESS Rozhraní");
        polozky.Should().OnlyContain(p => p.Cena == 94470m, "mezera i &nbsp; jsou oddělovač tisíců");
    }

    [Fact]
    public void Parse_EntityVNazvu_SeDekoduji()
    {
        RozpadLicenceParser.Parse("<tr><td>A &amp; B &ndash; C</td><td>1,50</td></tr>")
            .Should().ContainSingle().Which.Nazev.Should().Be("A & B – C");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<table></table>")]
    [InlineData("<tr><td>bez ceny</td><td>neni cislo</td></tr>")]
    public void Parse_NecitelnyVstup_VraciPrazdno(string? html)
    {
        RozpadLicenceParser.Parse(html).Should().BeEmpty();
    }
}
