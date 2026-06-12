using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramDateBlokBuilderTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private static ZaznamHarmonogramKrokEntity Row(int poradi, DateTime? plan, DateTime? skut)
        => new() { Poradi = (byte)poradi, PlanDatum = plan, SkutecnostDatum = skut };

    [Fact]
    public void BuildKroky_vraci_10_kroku_s_nazvy_z_konstanty()
    {
        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, Array.Empty<ZaznamHarmonogramKrokEntity>());

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

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows);

        var k1 = kroky.Single(k => k.KrokIndex == 1);
        k1.BaselineDatum.Should().Be(new DateTime(2026, 1, 10));
        k1.SkutecneDatum.Should().Be(new DateTime(2026, 1, 13));
        k1.OdchylkaDni.Should().Be(3);
    }

    [Fact]
    public void BuildKroky_nevyplneny_krok_ma_odchylku_null()
    {
        var rows = new[] { Row(1, new DateTime(2026, 1, 10), null) };

        var kroky = HarmonogramDateBlokBuilder.BuildKroky(Start, rows);

        kroky.Single(k => k.KrokIndex == 1).OdchylkaDni.Should().BeNull();
    }

    [Fact]
    public void BuildSouhrn_pocita_dokonceni_a_prekroceni()
    {
        var rows = new[]
        {
            Row(1, new DateTime(2026, 1, 10), new DateTime(2026, 1, 12)),
            Row(10, new DateTime(2026, 1, 25), new DateTime(2026, 1, 28)),
        };

        var souhrn = HarmonogramDateBlokBuilder.BuildSouhrn(
            Start, rows, termin: new DateTime(2026, 1, 25), today: new DateTime(2026, 2, 1));

        souhrn.BaselineDokonceni.Should().Be(new DateTime(2026, 1, 25));
        souhrn.SkutecneDokonceni.Should().Be(new DateTime(2026, 1, 28));
        souhrn.Stihame.Should().BeFalse();
        souhrn.PrekroceniDni.Should().Be(3);
    }
}
