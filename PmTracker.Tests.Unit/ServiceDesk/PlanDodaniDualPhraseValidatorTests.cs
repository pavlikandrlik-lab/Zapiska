using FluentAssertions;
using PmTracker.ServiceDesk.Contracts;
using PmTracker.Web.Services.ServiceDesk;
using Xunit;

namespace PmTracker.Tests.Unit.ServiceDesk;

/// <summary>
/// Pure-logic testy pro <see cref="PlanDodaniDualPhraseValidator"/>.
/// Spec: 2026-04-28-nes-vyjadreni-a-4-datumy-design §4.3.
/// </summary>
public sealed class PlanDodaniDualPhraseValidatorTests
{
    private static HotVyjadreniDto V(long id, DateTime datum, string popis)
        => new(id, "25", "PID-1", datum, "uzivatel", popis, "Tym", 1);

    [Fact]
    public void FilterValidated_PrazdnyVstup_VratiPrazdny()
    {
        PlanDodaniDualPhraseValidator.FilterValidated(Array.Empty<HotVyjadreniDto>())
            .Should().BeEmpty();
    }

    [Fact]
    public void FilterValidated_PlanDodaniBezNavazujiciK6_NeprojdeValidaci()
    {
        var input = new[]
        {
            V(1, new DateTime(2026, 1, 1), "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, new DateTime(2026, 1, 2), "Něco úplně jiného"),
        };

        PlanDodaniDualPhraseValidator.FilterValidated(input).Should().BeEmpty();
    }

    [Fact]
    public void FilterValidated_K6VeNplus1_ValidaceProjde()
    {
        var input = new[]
        {
            V(1, new DateTime(2026, 1, 1), "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, new DateTime(2026, 1, 2), "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var result = PlanDodaniDualPhraseValidator.FilterValidated(input);
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public void FilterValidated_K6VeNplus2_ValidaceProjde()
    {
        var input = new[]
        {
            V(1, new DateTime(2026, 1, 1), "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, new DateTime(2026, 1, 2), "Mezikrok"),
            V(3, new DateTime(2026, 1, 3), "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var result = PlanDodaniDualPhraseValidator.FilterValidated(input);
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public void FilterValidated_K6VeNplus3_ValidaceSelze()
    {
        var input = new[]
        {
            V(1, new DateTime(2026, 1, 1), "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, new DateTime(2026, 1, 2), "Mezikrok 1"),
            V(3, new DateTime(2026, 1, 3), "Mezikrok 2"),
            V(4, new DateTime(2026, 1, 4), "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        PlanDodaniDualPhraseValidator.FilterValidated(input).Should().BeEmpty();
    }

    [Fact]
    public void FilterValidated_DvaPlanDodaniValidovane_VratiOba()
    {
        var input = new[]
        {
            V(1, new DateTime(2026, 1, 1), "PM předal záznam dodavateli : ABC s termínem plnění dodavatele 15.4.2026"),
            V(2, new DateTime(2026, 1, 2), "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
            V(3, new DateTime(2026, 2, 1), "VS ISSP předal záznam dodavateli : XYZ s termínem plnění dodavatele 30.6.2026"),
            V(4, new DateTime(2026, 2, 2), "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var result = PlanDodaniDualPhraseValidator.FilterValidated(input);
        result.Should().HaveCount(2);
        result.Select(r => r.Id).Should().Equal(1, 3);
    }

    [Fact]
    public void FilterValidated_PouzeJednaCastPlanDodaniFraze_NenajdeKandidaty()
    {
        // Jen "předal záznam dodavateli :" bez druhé části → není PlanDodani fráze
        var input = new[]
        {
            V(1, new DateTime(2026, 1, 1), "PM předal záznam dodavateli : ABC bez termínu"),
            V(2, new DateTime(2026, 1, 2), "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        PlanDodaniDualPhraseValidator.FilterValidated(input).Should().BeEmpty();
    }

    [Fact]
    public void FilterValidated_HtmlWrappers_ProchaziSubstringMatch()
    {
        // Real-world HTML obal — predikát musí matchnout přes <b>, <br> apod.
        var input = new[]
        {
            V(1, new DateTime(2026, 1, 1),
                "<br>VS FIS předal záznam dodavateli : <b>ABC</b> s termínem plnění dodavatele <b>31.03.2026</b>"),
            V(2, new DateTime(2026, 1, 2),
                "Záznam byl předán dodavateli k řešení. Kalkulace byla akceptována."),
        };

        var result = PlanDodaniDualPhraseValidator.FilterValidated(input);
        result.Should().HaveCount(1);
    }
}
