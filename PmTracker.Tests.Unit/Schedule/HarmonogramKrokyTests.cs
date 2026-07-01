using FluentAssertions;
using PmTracker.Web.Services.Schedules;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class HarmonogramKrokyTests
{
    [Fact]
    public void Vsech_10_kroku_je_definovano_v_poradi_1_az_10()
    {
        HarmonogramKroky.Vse.Select(k => k.Poradi).Should().Equal(Enumerable.Range(1, 10));
    }

    [Fact]
    public void Manualni_kroky_jsou_2_5_8_9()
    {
        HarmonogramKroky.Vse.Where(k => k.JeManualni).Select(k => k.Poradi)
            .Should().Equal(2, 5, 8, 9);
    }

    [Theory]
    [InlineData(1, "K1")]
    [InlineData(3, "K3")]
    [InlineData(4, "K4_K7")]
    [InlineData(6, "K6")]
    [InlineData(7, "K4_K7")]
    [InlineData(10, "K10")]
    public void Harvestovane_kroky_maji_spravny_predikat(int poradi, string predikat)
    {
        HarmonogramKroky.Vse.Single(k => k.Poradi == poradi).HarvestPredikat.Should().Be(predikat);
    }

    // Barvy kroků (2026-06-29): dle Excelu PMP = MS Office „Zelená, zvýraznění 6" (Accent6 #70AD47),
    // PNF = „Zlatá, zvýraznění 4" (Accent4 #FFC000). PMP = kroky 1–5, PNF = kroky 6–10
    // (stejné rozdělení jako stepper ve VyjadreniModalViewModelBuilder; krok 1 = sdílený start → PMP).
    // V každé skupině jemný přechod od „světlá 80 %" k „světlá 40 %" (lineární interpolace přes 5 kroků).
    [Theory]
    [InlineData(1, "#E2EFDA")]
    [InlineData(2, "#D4E7C7")]
    [InlineData(3, "#C6E0B4")]
    [InlineData(4, "#B7D8A1")]
    [InlineData(5, "#A9D08E")]
    [InlineData(6, "#FFF2CC")]
    [InlineData(7, "#FFECB3")]
    [InlineData(8, "#FFE699")]
    [InlineData(9, "#FFDF80")]
    [InlineData(10, "#FFD966")]
    public void Kroky_maji_PMP_zelenou_a_PNF_zlatou_odstinenou_paletu(int poradi, string barvaHex)
    {
        HarmonogramKroky.Vse.Single(k => k.Poradi == poradi).BarvaHex.Should().Be(barvaHex);
    }
}
