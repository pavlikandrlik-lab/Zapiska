using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramDateBlokBuilderTests
{
    private static readonly DateTime Start = new(2026, 1, 1);
    private static readonly DateTime Today = new(2026, 2, 1);

    private static ZaznamHarmonogramKrokEntity Row(int poradi, DateTime? plan, DateTime? skut)
        => new() { Poradi = (byte)poradi, PlanDatum = plan, SkutecnostDatum = skut };

    [Fact]
    public void BuildKroky_vraci_10_kroku_s_nazvy_z_konstanty()
    {
        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, Array.Empty<ZaznamHarmonogramKrokEntity>(), Today);

        kroky.Should().HaveCount(10);
        kroky[0].KrokIndex.Should().Be(1);
        kroky[0].Nazev.Should().Be("1. priprava zadani dodavateli");
    }

    [Fact]
    public void BuildKroky_vyplneny_krok_ma_odchylku_jako_skut_minus_plan()
    {
        var rows = new[]
        {
            Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 13)), // +3 dny
        };

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows, Today);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.BaselineDatum.Should().Be(new DateTime(2026, 1, 10));
        k1.SkutecneDatum.Should().Be(new DateTime(2026, 1, 13));
        k1.OdchylkaDni.Should().Be(3);
        k1.Stav.Should().Be(HarmonogramKrokStav.Splneno);
    }

    [Fact]
    public void BuildKroky_nevyplneny_krok_ma_null_SkutecneDatum_zadny_PlanEnd_fallback()
    {
        var rows = new[] { Row(1, new DateTime(2026, 1, 10), null) };

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows, Today);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.OdchylkaDni.Should().BeNull();
        k1.SkutecneDatum.Should().BeNull("zrušen PlanEnd fallback (zdroj zkreslení skutečnost=plán)");
        k1.Stav.Should().Be(HarmonogramKrokStav.VProdleni, "plán 10.1. < dnes 1.2. a nevyplněno");
    }

    [Fact]
    public void BuildSouhrn_ma_aktualni_krok_a_znamenkove_prekroceni()
    {
        // krok 1 vyplněn, krok 2 (a dál) nevyplněn → aktuální = 2
        var rows = new[]
        {
            Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 11)),
            Row(2, new DateTime(2026, 1, 20), null),
        };

        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
            Start, rows, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 2, 1));

        souhrn.AktualniKrokPoradi.Should().Be(2);
        souhrn.AktualniKrokNazev.Should().NotBeNullOrEmpty();
        souhrn.PrekroceniDni.Should().Be((new DateTime(2026, 2, 1) - new DateTime(2026, 1, 20)).Days);
        souhrn.Dokonceno.Should().BeFalse();
    }

    [Fact]
    public void BuildSouhrn_vse_vyplnene_je_dokonceno()
    {
        var rows = Enumerable.Range(1, 10)
            .Select(i => Row(i, new DateTime(2026, 1, 1).AddDays(i), new DateTime(2026, 1, 1).AddDays(i)))
            .ToArray();

        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
            Start, rows, termin: new DateTime(2026, 12, 1), today: new DateTime(2026, 2, 1));

        souhrn.Dokonceno.Should().BeTrue();
        souhrn.AktualniKrokPoradi.Should().BeNull();
        souhrn.PrekroceniDni.Should().Be(0);
    }
}
