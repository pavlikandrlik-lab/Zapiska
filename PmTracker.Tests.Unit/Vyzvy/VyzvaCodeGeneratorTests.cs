using FluentAssertions;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaCodeGeneratorTests
{
    [Fact]
    public void Generuj_FormatujeJakoPoradoveLomitkoRok()
        => VyzvaCodeGenerator.Generuj(poradoveVRoce: 2, rok: 2026).Should().Be("2/2026");

    // DalsiPoradoveVRoce zaniklo 2026-09-07 — čísla výzev se domlouvají se SVA
    // a zadávají ručně, aplikace další číslo negeneruje (spec §5.1).
}
