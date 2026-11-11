using System;
using FluentAssertions;
using PmTracker.Web.Services.ProjectDashboard.Zakladni;
using Xunit;

namespace PmTracker.Tests.Unit.Dashboard.Zakladni;

public sealed class ObdobiTests
{
    [Fact]
    public void Rok_SpansWholeYearInclusive()
    {
        var o = Obdobi.Rok(2025);
        o.Start.Should().Be(new DateTime(2025, 1, 1, 0, 0, 0));
        o.End.Should().Be(new DateTime(2025, 12, 31, 23, 59, 59));
        o.Label.Should().Be("2025");
    }

    [Fact]
    public void Kvartal_Q3_SpansJulToSep()
    {
        var o = Obdobi.Kvartal(2025, 3);
        o.Start.Should().Be(new DateTime(2025, 7, 1, 0, 0, 0));
        o.End.Should().Be(new DateTime(2025, 9, 30, 23, 59, 59));
        o.Label.Should().Be("Q3 2025");
    }

    [Fact]
    public void Rozsah_UsesGivenBoundsAndEndOfDay()
    {
        var o = Obdobi.Rozsah(new DateTime(2024, 2, 10), new DateTime(2024, 5, 20));
        o.Start.Should().Be(new DateTime(2024, 2, 10, 0, 0, 0));
        o.End.Should().Be(new DateTime(2024, 5, 20, 23, 59, 59));
        o.Label.Should().Be("10.02.2024 – 20.05.2024");
    }

    [Fact]
    public void Kvartal_InvalidQuarter_Throws()
    {
        var act = () => Obdobi.Kvartal(2025, 5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Predchozi_rok()
    {
        var p = Obdobi.Rok(2026).Predchozi();
        p.Should().Be(Obdobi.Rok(2025));
        p.Druh.Should().Be(ObdobiDruh.Rok);
    }

    [Fact]
    public void Predchozi_Q1_je_Q4_minuleho_roku()
    {
        Obdobi.Kvartal(2026, 1).Predchozi().Should().Be(Obdobi.Kvartal(2025, 4));
        Obdobi.Kvartal(2026, 3).Predchozi().Should().Be(Obdobi.Kvartal(2026, 2));
    }

    [Fact]
    public void Predchozi_rozsah_stejne_delky()
    {
        var o = Obdobi.Rozsah(new DateTime(2026, 3, 11), new DateTime(2026, 3, 20));
        var p = o.Predchozi();

        p.Druh.Should().Be(ObdobiDruh.Rozsah);
        p.Start.Should().Be(new DateTime(2026, 3, 1));
        p.End.Should().Be(new DateTime(2026, 3, 10, 23, 59, 59));
        (p.End - p.Start).Should().Be(o.End - o.Start);
    }
}
