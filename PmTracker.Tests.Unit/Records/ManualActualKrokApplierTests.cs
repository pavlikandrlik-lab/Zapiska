using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Data;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Records;

public sealed class ManualActualKrokApplierTests
{
    private static HarmonogramVypocetKroku Krok(int poradi, DateTime planEnd)
        => new(
            KrokIndex: poradi,
            Kod: $"HS{poradi:D2}_DURATION",
            Nazev: $"Krok {poradi}",
            BarvaHex: "#000",
            TrvaniTypId: 100 + poradi,
            ZpozdeniTypId: 200 + poradi,
            TrvaniDni: 5,
            ZpozdeniDni: 0,
            PlanStartDatum: planEnd.AddDays(-5),
            BaselineDatum: planEnd,
            RealStartDatum: planEnd.AddDays(-5),
            PosunuteDatum: planEnd);

    [Fact]
    public void Compute_Empty_ShouldReturnEmpty()
    {
        var result = ManualActualKrokApplier.Compute(
            Array.Empty<ManualActualKrokDto>(),
            Array.Empty<HarmonogramVypocetKroku>(),
            new Dictionary<Guid, int>(),
            new Dictionary<Guid, int>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compute_SkutecnostRovnaPlanu_ShouldYieldZeroOdchylka()
    {
        var k2 = Guid.NewGuid();
        var planEnd = new DateTime(2026, 3, 20);
        var result = ManualActualKrokApplier.Compute(
            new[]
            {
                new ManualActualKrokDto { KrokKey = k2, AbsolutniDatum = DateOnly.FromDateTime(planEnd) }
            },
            new[] { Krok(2, planEnd) },
            new Dictionary<Guid, int> { [k2] = 2 },
            new Dictionary<Guid, int> { [k2] = 202 });

        result.Should().HaveCount(1);
        result[0].OdchylkaDni.Should().Be(0);
        result[0].DelayTypId.Should().Be(202);
        result[0].KrokPoradi.Should().Be(2);
    }

    [Fact]
    public void Compute_SkutecnostPoPlanu_ShouldYieldPositiveOdchylka()
    {
        var k5 = Guid.NewGuid();
        var planEnd = new DateTime(2026, 3, 20);
        var actual = DateOnly.FromDateTime(planEnd).AddDays(3);
        var result = ManualActualKrokApplier.Compute(
            new[]
            {
                new ManualActualKrokDto { KrokKey = k5, AbsolutniDatum = actual }
            },
            new[] { Krok(5, planEnd) },
            new Dictionary<Guid, int> { [k5] = 5 },
            new Dictionary<Guid, int> { [k5] = 205 });

        result[0].OdchylkaDni.Should().Be(3);
    }

    [Fact]
    public void Compute_SkutecnostPredPlanem_ShouldYieldNegativeOdchylka()
    {
        var k8 = Guid.NewGuid();
        var planEnd = new DateTime(2026, 3, 20);
        var actual = DateOnly.FromDateTime(planEnd).AddDays(-7);
        var result = ManualActualKrokApplier.Compute(
            new[]
            {
                new ManualActualKrokDto { KrokKey = k8, AbsolutniDatum = actual }
            },
            new[] { Krok(8, planEnd) },
            new Dictionary<Guid, int> { [k8] = 8 },
            new Dictionary<Guid, int> { [k8] = 208 });

        result[0].OdchylkaDni.Should().Be(-7);
    }

    [Fact]
    public void Compute_UnknownKrokKey_ShouldThrow()
    {
        var act = () => ManualActualKrokApplier.Compute(
            new[] { new ManualActualKrokDto { KrokKey = Guid.NewGuid(), AbsolutniDatum = new DateOnly(2026, 3, 1) } },
            Array.Empty<HarmonogramVypocetKroku>(),
            new Dictionary<Guid, int>(),
            new Dictionary<Guid, int>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*není ve schématu*");
    }

    [Fact]
    public void Compute_NonManualStep_ShouldThrow()
    {
        var k1 = Guid.NewGuid();
        var act = () => ManualActualKrokApplier.Compute(
            new[] { new ManualActualKrokDto { KrokKey = k1, AbsolutniDatum = new DateOnly(2026, 3, 1) } },
            new[] { Krok(1, new DateTime(2026, 3, 1)) },
            new Dictionary<Guid, int> { [k1] = 1 },
            new Dictionary<Guid, int> { [k1] = 201 });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*není mezi kroky*");
    }

    [Fact]
    public void Compute_MissingDelayTypId_ShouldThrow()
    {
        var k2 = Guid.NewGuid();
        var act = () => ManualActualKrokApplier.Compute(
            new[] { new ManualActualKrokDto { KrokKey = k2, AbsolutniDatum = new DateOnly(2026, 3, 1) } },
            new[] { Krok(2, new DateTime(2026, 3, 1)) },
            new Dictionary<Guid, int> { [k2] = 2 },
            new Dictionary<Guid, int> { [k2] = 0 });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HS0X_DELAY*");
    }
}
