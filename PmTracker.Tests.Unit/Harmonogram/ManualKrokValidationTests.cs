using FluentAssertions;
using PmTracker.Web.Models.ViewModels;
using PmTracker.Web.Services.Records;

namespace PmTracker.Tests.Unit.Harmonogram;

public sealed class ManualKrokValidationTests
{
    private static readonly DateOnly Today = new(2026, 4, 22);

    [Fact]
    public void ValidateManualActualKroky_Empty_ShouldNotThrow()
    {
        var act = () => ManualProposalFieldValidator.ValidateManualActualKroky(Array.Empty<ManualActualKrokDto>(), Today);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateManualActualKroky_HappyPath_ShouldNotThrow()
    {
        var act = () => ManualProposalFieldValidator.ValidateManualActualKroky(
            new[]
            {
                new ManualActualKrokDto { KrokKey = Guid.NewGuid(), AbsolutniDatum = new DateOnly(2026, 3, 15) },
                new ManualActualKrokDto { KrokKey = Guid.NewGuid(), AbsolutniDatum = new DateOnly(2026, 4, 22) }
            },
            Today);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateManualActualKroky_EmptyKrokKey_ShouldThrow()
    {
        var act = () => ManualProposalFieldValidator.ValidateManualActualKroky(
            new[]
            {
                new ManualActualKrokDto { KrokKey = Guid.Empty, AbsolutniDatum = new DateOnly(2026, 3, 1) }
            },
            Today);

        act.Should().Throw<PmTracker.Web.Services.Data.RecordValidationException>()
            .WithMessage("*identifikátor*");
    }

    [Fact]
    public void ValidateManualActualKroky_Duplicate_ShouldThrow()
    {
        var krokKey = Guid.NewGuid();
        var act = () => ManualProposalFieldValidator.ValidateManualActualKroky(
            new[]
            {
                new ManualActualKrokDto { KrokKey = krokKey, AbsolutniDatum = new DateOnly(2026, 3, 1) },
                new ManualActualKrokDto { KrokKey = krokKey, AbsolutniDatum = new DateOnly(2026, 3, 5) }
            },
            Today);

        act.Should().Throw<PmTracker.Web.Services.Data.RecordValidationException>()
            .WithMessage("*jen jedno*");
    }

    /// <summary>
    /// FIX 2026-05-04: per user instruction "v harmonogramu neměl být plán a skutečnost nijak
    /// vzájemně propojeny omezeními" — future-date validace v <c>ManualProposalFieldValidator</c>
    /// byla odstraněna. Test změněn na opačnou polaritu: future datum NEhází (decoupling plán↔skutečnost).
    /// </summary>
    [Fact]
    public void ValidateManualActualKroky_FutureDate_IsAllowed()
    {
        var act = () => ManualProposalFieldValidator.ValidateManualActualKroky(
            new[]
            {
                new ManualActualKrokDto { KrokKey = Guid.NewGuid(), AbsolutniDatum = Today.AddDays(1) }
            },
            Today);

        act.Should().NotThrow("future datum skutečnosti je povolené po odstranění plán↔skutečnost validací");
    }

    [Fact]
    public void ValidateManualActualKroky_TodayAllowed()
    {
        var act = () => ManualProposalFieldValidator.ValidateManualActualKroky(
            new[]
            {
                new ManualActualKrokDto { KrokKey = Guid.NewGuid(), AbsolutniDatum = Today }
            },
            Today);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHarmonogramVazby_Empty_ShouldNotThrow()
    {
        var act = () => ManualProposalFieldValidator.ValidateHarmonogramVazby(Array.Empty<HarmonogramVazbaDto>(), externiVazbyCount: 0);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHarmonogramVazby_HappyPath_ShouldNotThrow()
    {
        var act = () => ManualProposalFieldValidator.ValidateHarmonogramVazby(
            new[]
            {
                new HarmonogramVazbaDto
                {
                    KrokKey = Guid.NewGuid(),
                    ExterniOdkazIndex = 0,
                    HotVyjadreniId = 1,
                    DatumVyjadreni = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
                new HarmonogramVazbaDto
                {
                    KrokKey = Guid.NewGuid(),
                    ExterniOdkazIndex = 1,
                    HotVyjadreniId = 2,
                    DatumVyjadreni = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
                }
            },
            externiVazbyCount: 2);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateHarmonogramVazby_OutOfRangeIndex_ShouldThrow()
    {
        var act = () => ManualProposalFieldValidator.ValidateHarmonogramVazby(
            new[]
            {
                new HarmonogramVazbaDto
                {
                    KrokKey = Guid.NewGuid(),
                    ExterniOdkazIndex = 3,
                    HotVyjadreniId = 1
                }
            },
            externiVazbyCount: 2);

        act.Should().Throw<PmTracker.Web.Services.Data.RecordValidationException>()
            .WithMessage("*mimo rozsah*");
    }

    [Fact]
    public void ValidateHarmonogramVazby_NonPositiveHotId_ShouldThrow()
    {
        var act = () => ManualProposalFieldValidator.ValidateHarmonogramVazby(
            new[]
            {
                new HarmonogramVazbaDto
                {
                    KrokKey = Guid.NewGuid(),
                    ExterniOdkazIndex = 0,
                    HotVyjadreniId = 0
                }
            },
            externiVazbyCount: 1);

        act.Should().Throw<PmTracker.Web.Services.Data.RecordValidationException>()
            .WithMessage("*kladné*");
    }

    [Fact]
    public void ValidateHarmonogramVazby_DuplicateKrokKey_ShouldThrow()
    {
        var krokKey = Guid.NewGuid();
        var act = () => ManualProposalFieldValidator.ValidateHarmonogramVazby(
            new[]
            {
                new HarmonogramVazbaDto { KrokKey = krokKey, ExterniOdkazIndex = 0, HotVyjadreniId = 1 },
                new HarmonogramVazbaDto { KrokKey = krokKey, ExterniOdkazIndex = 0, HotVyjadreniId = 2 }
            },
            externiVazbyCount: 1);

        act.Should().Throw<PmTracker.Web.Services.Data.RecordValidationException>()
            .WithMessage("*jen jednu bublinu*");
    }

    [Fact]
    public void HarmonogramManualSteps_ContainsExpectedSteps()
    {
        HarmonogramManualSteps.IsManual(2).Should().BeTrue();
        HarmonogramManualSteps.IsManual(5).Should().BeTrue();
        HarmonogramManualSteps.IsManual(8).Should().BeTrue();
        HarmonogramManualSteps.IsManual(9).Should().BeTrue();
        HarmonogramManualSteps.IsManual(1).Should().BeFalse();
        HarmonogramManualSteps.IsManual(3).Should().BeFalse();
        HarmonogramManualSteps.IsManual(4).Should().BeFalse();
        HarmonogramManualSteps.IsManual(6).Should().BeFalse();
        HarmonogramManualSteps.IsManual(7).Should().BeFalse();
        HarmonogramManualSteps.IsManual(10).Should().BeFalse();
    }
}
