using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk.Prodleni;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk.Prodleni;

public sealed class CeskeSvatkyTests
{
    private readonly CeskeSvatky _s = new();

    [Theory]
    [InlineData(2016, 3, 25)] [InlineData(2024, 3, 29)] [InlineData(2025, 4, 18)]
    [InlineData(2026, 4, 3)]  [InlineData(2027, 3, 26)] [InlineData(2030, 4, 19)]
    public void Velky_patek_od_2016_neni_pracovni(int y, int m, int d)
        => _s.JePracovniDen(new DateTime(y, m, d)).Should().BeFalse();

    [Fact] public void Velky_patek_2015_je_pracovni() // svátkem až od 2016
        => _s.JePracovniDen(new DateTime(2015, 4, 3)).Should().BeTrue();

    [Theory]
    [InlineData(2024, 4, 1)] [InlineData(2025, 4, 21)] [InlineData(2026, 4, 6)] [InlineData(2027, 3, 29)]
    public void Velikonocni_pondeli_neni_pracovni(int y, int m, int d)
        => _s.JePracovniDen(new DateTime(y, m, d)).Should().BeFalse();

    [Theory]
    [InlineData(1, 1)] [InlineData(5, 1)] [InlineData(5, 8)] [InlineData(7, 5)] [InlineData(7, 6)]
    [InlineData(9, 28)] [InlineData(10, 28)] [InlineData(11, 17)] [InlineData(12, 24)] [InlineData(12, 25)] [InlineData(12, 26)]
    public void Pevne_svatky_2026(int m, int d)
        => _s.JePracovniDen(new DateTime(2026, m, d)).Should().BeFalse();

    [Fact] public void Vikend_neni_pracovni()
        => _s.JePracovniDen(new DateTime(2026, 3, 7)).Should().BeFalse(); // sobota

    [Fact] public void Bezny_utery_je_pracovni()
        => _s.JePracovniDen(new DateTime(2026, 3, 3, 15, 30, 0)).Should().BeTrue(); // čas se ignoruje
}
