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

    // Real-world shape: ServiceDesk popis texty obsahují HTML wrappery (<b>, <br>, <BR>,
    // <li>, &nbsp; prefixy) a občas jsou roztažené přes několik řádků. Klasifikátor musí
    // tyto varianty zvládnout — substring match + case-insensitive.
    [Theory]
    // K10 s &nbsp; prefixem a <BR> suffixem (typický Automat + PM tvar)
    [InlineData("&nbsp;Záznam byl převeden do archivu. <BR><B>Záznam byl převzat k řešení dne : 07.04.2026 7:27:00</B>", HarvestPredicateKind.K10_NasazeniArchivace)]
    // PlanDodani s <b> tagy kolem dodavatele + termínu (dvousložkový predikát)
    [InlineData("<br>VS FIS předal záznam dodavateli : <b>DODAVATEL</b> s termínem plnění dodavatele <b>31.03.2026</b>", HarvestPredicateKind.PlanDodani)]
    // K6: Automat-generovaný, bez HTML
    [InlineData("Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována.", HarvestPredicateKind.K6_OdeslaniPozadavku)]
    // K3: Automat-generovaný s ticketconnector značkou
    [InlineData("Záznam byl založen a předán dodavateli k řešení pod značkou: PO99999.", HarvestPredicateKind.K3_OdeslaniZadaniPmp)]
    // K4/K7: Dodavatel přidal řešení s <i> tagem + pokračování textu
    [InlineData("<i>Dodavatel přidal řešení:</i><br>Úprava byla realizována do verze XYZ.", HarvestPredicateKind.K4_K7_DodaniReseni)]
    // Automat „Záznam byl převeden u dodavatele do archivu." je jiná věta, NESMÍ chytit K10
    [InlineData("Záznam byl převeden u dodavatele do archivu.", HarvestPredicateKind.None)]
    // Automat „Záznam byl převzat od dodavatele k řešení." — žádný workflow event, NESMÍ chytit nic
    [InlineData("Záznam byl převzat od dodavatele k řešení.", HarvestPredicateKind.None)]
    // Čistá technická značka (id_kalk) — K typ bubliny, ale plain text bez workflow fráze
    [InlineData("26050322003966", HarvestPredicateKind.None)]
    public void ClassifyPopis_RealWorldHtmlShapes_MatchesExpectedPredicate(string popis, HarvestPredicateKind expected)
    {
        HarvestPredicates.ClassifyPopis(popis).Should().Be(expected);
    }

    // -------- ClassifyPopisForNes — NES-specific klasifikace --------

    [Fact]
    public void ClassifyPopisForNes_NesObjednaniFraze_VraciNesDatumObjednani()
    {
        var result = HarvestPredicates.ClassifyPopisForNes(
            "Dnes 28.4.2026: Záznam byl předán dodavateli k řešení.");
        result.Should().Be(HarvestPredicateKind.NES_DatumObjednani);
    }

    [Fact]
    public void ClassifyPopisForNes_NesDodaniFraze_VraciNesDatumDodani()
    {
        var result = HarvestPredicates.ClassifyPopisForNes(
            "Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 123456 byla vytvořena.");
        result.Should().Be(HarvestPredicateKind.NES_DatumDodani);
    }

    [Fact]
    public void ClassifyPopisForNes_K10Fraze_VraciK10()
    {
        var result = HarvestPredicates.ClassifyPopisForNes("Záznam byl převeden do archivu.");
        result.Should().Be(HarvestPredicateKind.K10_NasazeniArchivace);
    }

    [Fact]
    public void ClassifyPopisForNes_PrazdnyPopis_VraciNone()
    {
        HarvestPredicates.ClassifyPopisForNes("").Should().Be(HarvestPredicateKind.None);
        HarvestPredicates.ClassifyPopisForNes(null).Should().Be(HarvestPredicateKind.None);
        HarvestPredicates.ClassifyPopisForNes("Něco jiného.").Should().Be(HarvestPredicateKind.None);
    }

    [Fact]
    public void ClassifyPopisForNes_K10ManaPredNesDodani()
    {
        // Pokud v popisu je K10 i NES dodání zároveň, K10 vyhrává (specifičtější).
        var text = "Záznam byl převeden do archivu. Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo 1.";
        HarvestPredicates.ClassifyPopisForNes(text).Should().Be(HarvestPredicateKind.K10_NasazeniArchivace);
    }

    [Fact]
    public void GetSqlLikePattern_NesDatumObjednani_ReturnsExpected()
    {
        HarvestPredicates.GetSqlLikePattern(HarvestPredicateKind.NES_DatumObjednani)
            .Should().Be("%Záznam byl předán dodavateli k řešení.%");
    }

    [Fact]
    public void GetSqlLikePattern_NesDatumDodani_ReturnsExpected()
    {
        HarvestPredicates.GetSqlLikePattern(HarvestPredicateKind.NES_DatumDodani)
            .Should().Be("%Vazba na IMPLEMENTAČNÍ ZÁZNAM HOTLINE číslo%");
    }
}
