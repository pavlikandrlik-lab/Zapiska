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
}
