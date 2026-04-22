using FluentAssertions;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

public sealed class HarvestPredicatesTests
{
    [Theory]
    [InlineData("Záznam byl založen a předán dodavateli k řešení pod značkou: XYZ", HarvestPredicateKind.K3_OdeslaniZadaniPmp)]
    [InlineData("Projektový manažer - FIS předal záznam dodavateli : Atos s.r.o. s termínem plnění dodavatele 31.3.2026", HarvestPredicateKind.PlanDodani)]
    [InlineData("Dodavatel přidal řešení.", HarvestPredicateKind.K4_K7_DodaniReseni)]
    [InlineData("Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.", HarvestPredicateKind.K6_OdeslaniPozadavku)]
    [InlineData("Záznam byl převeden do archivu.", HarvestPredicateKind.K10_NasazeniArchivace)]
    public void ClassifyPopis_KnownPhrase_ReturnsExpectedKind(string popis, HarvestPredicateKind expected)
    {
        HarvestPredicates.ClassifyPopis(popis).Should().Be(expected);
    }

    [Fact]
    public void ClassifyPopis_UnknownText_ReturnsNone()
        => HarvestPredicates.ClassifyPopis("Random nepovinný text").Should().Be(HarvestPredicateKind.None);

    [Fact]
    public void ClassifyPopis_Null_ReturnsNone()
        => HarvestPredicates.ClassifyPopis(null).Should().Be(HarvestPredicateKind.None);

    [Fact]
    public void ClassifyPopis_Empty_ReturnsNone()
        => HarvestPredicates.ClassifyPopis("").Should().Be(HarvestPredicateKind.None);

    [Fact]
    public void ClassifyPopis_CaseInsensitive()
        => HarvestPredicates.ClassifyPopis("DODAVATEL přidal ŘEŠENÍ").Should().Be(HarvestPredicateKind.K4_K7_DodaniReseni);

    [Fact]
    public void ClassifyPopis_ArchivOverK4WhenBothPresent()
    {
        // Archiv má přednost — specifičtější predikát
        var text = "Záznam byl převeden do archivu. Dodavatel přidal řešení.";
        HarvestPredicates.ClassifyPopis(text).Should().Be(HarvestPredicateKind.K10_NasazeniArchivace);
    }

    [Fact]
    public void GetSqlLikePattern_K6_ReturnsExpected()
    {
        HarvestPredicates.GetSqlLikePattern(HarvestPredicateKind.K6_OdeslaniPozadavku)
            .Should().Be("%Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.%");
    }

    [Fact]
    public void GetSqlLikePattern_PlanDodani_HasTwoWildcardParts()
    {
        var p = HarvestPredicates.GetSqlLikePattern(HarvestPredicateKind.PlanDodani);
        p.Should().Contain("předal záznam dodavateli :");
        p.Should().Contain("s termínem plnění dodavatele");
    }

    [Fact]
    public void GetSqlLikePattern_None_Throws()
    {
        var act = () => HarvestPredicates.GetSqlLikePattern(HarvestPredicateKind.None);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
