using FluentAssertions;
using PmTracker.Web.Services.Schedules;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Plán 4 Feature C Task 2 — matice (TypZaznamu × KrokPoradi) → PredikatKey.
/// Zdroj pravdy: docs/superpowers/specs/2026-04-21-servicedesk-vytezovani-vyjadreni-design.md §3/3.1.
///
/// Přehled aktivních automatických kroků (nejsou „ruční" dropdown kroky):
///  - NES: {1} — K1 = datum_zalozeni (netýká se vyjádření)
///  - PMP: {1, 3, 4} — K3 "odeslání dodavateli", K4 "dodání návrhu řešení"
///  - PNF: {1, 6, 7, 10} — K6 "odeslání požadavku na výrobu (kalkulace akceptována)",
///                         K7 "dodání funkcionality", K10 "nasazení do provozu/archivace"
///
/// Predikátové klíče odpovídají <see cref="PmTracker.Web.Services.ServiceDesk.HarvestPredicateKind"/>:
///  - K3 = K3_OdeslaniZadaniPmp
///  - K4 a K7 společně = K4_K7_DodaniReseni (výstup se liší podle typu: PMP→4, PNF→7)
///  - K6 = K6_OdeslaniPozadavku
///  - K10 = K10_NasazeniArchivace
/// </summary>
public sealed class HarmonogramKrokDatumMappingTests
{
    [Theory]
    // NES — odpojen od harmonogramu (2026-04-28 spec): žádné mapování ani pro krok 1
    [InlineData("NES", 1, null)]
    [InlineData("NES", 3, null)]
    [InlineData("NES", 6, null)]
    [InlineData("NES", 10, null)]
    // PMP — K1 (datum založení), K3 (odeslání), K4 (dodání řešení)
    [InlineData("PMP", 1, "K1")]
    [InlineData("PMP", 3, "K3")]
    [InlineData("PMP", 4, "K4_K7")]
    // PNF — K1 (datum založení), K6 (odeslání), K7 (dodání), K10 (archivace)
    [InlineData("PNF", 1, "K1")]
    [InlineData("PNF", 6, "K6")]
    [InlineData("PNF", 7, "K4_K7")]
    [InlineData("PNF", 10, "K10")]
    public void GetPredikatKey_MapujeDleSpecMatice(string typ, int krok, string? expected)
    {
        HarmonogramKrokDatumMapping.GetPredikatKey(typ, krok).Should().Be(expected);
    }

    [Theory]
    // Kroky mimo automatickou sadu daného typu → null (ruční kroky 2/5/8/9 + kroky nepatřící typu)
    [InlineData("PMP", 2)]   // ruční krok
    [InlineData("PMP", 5)]   // ruční krok
    [InlineData("PMP", 6)]   // PMP nemá K6
    [InlineData("PMP", 10)]  // PMP nemá K10 (viz §3.2)
    [InlineData("PNF", 3)]   // PNF nemá K3
    [InlineData("PNF", 4)]   // PNF nemá K4
    [InlineData("PNF", 8)]   // ruční krok
    [InlineData("PNF", 9)]   // ruční krok
    [InlineData("XYZ", 3)]   // neznámý typ
    [InlineData("", 3)]
    public void GetPredikatKey_NeznamyVstup_VraciNull(string typ, int krok)
    {
        HarmonogramKrokDatumMapping.GetPredikatKey(typ, krok).Should().BeNull();
    }

    [Fact]
    public void GetPredikatKey_NullTyp_VraciNull()
    {
        HarmonogramKrokDatumMapping.GetPredikatKey(null!, 3).Should().BeNull();
    }

    [Theory]
    [InlineData("nes", 1, null)]      // lowercase NES — odpojen, žádné mapování ani pro K1
    [InlineData("pmp", 1, "K1")]      // lowercase PMP — krok 1 K1 mapping
    [InlineData("pmp", 3, "K3")]      // lowercase — normalizuje se na upper
    [InlineData("  PNF  ", 6, "K6")]  // trim
    public void GetPredikatKey_NormalizujeTypZaznamu(string typ, int krok, string? expected)
    {
        HarmonogramKrokDatumMapping.GetPredikatKey(typ, krok).Should().Be(expected);
    }

    [Fact]
    public void SupportedTypes_Obsahuje3Typy()
    {
        HarmonogramKrokDatumMapping.SupportedTypes.Should().BeEquivalentTo(new[] { "NES", "PMP", "PNF" });
    }

    [Fact]
    public void IsAutomatickyKrok_VraciTruePropouzeKrokysMatchemMaticeTypu()
    {
        HarmonogramKrokDatumMapping.IsAutomatickyKrok("PMP", 1).Should().BeTrue();   // K1 datum založení (NEW 2026-04-28)
        HarmonogramKrokDatumMapping.IsAutomatickyKrok("PMP", 3).Should().BeTrue();
        HarmonogramKrokDatumMapping.IsAutomatickyKrok("PMP", 2).Should().BeFalse();
        HarmonogramKrokDatumMapping.IsAutomatickyKrok("PNF", 1).Should().BeTrue();   // K1 datum založení (NEW 2026-04-28)
        HarmonogramKrokDatumMapping.IsAutomatickyKrok("NES", 1).Should().BeFalse();  // NES odpojen
    }
}
