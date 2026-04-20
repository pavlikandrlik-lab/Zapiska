using FluentAssertions;
using PmTracker.Web.Services.Vyzvy;
using Xunit;

namespace PmTracker.Tests.Unit.Vyzvy;

public sealed class VyzvaCodeGeneratorTests
{
    [Fact]
    public void Generuj_FormatujeJakoPoradoveLomitkoRok()
        => VyzvaCodeGenerator.Generuj(poradoveVRoce: 2, rok: 2026).Should().Be("2/2026");

    [Fact]
    public void DalsiPoradoveVRoce_Prazdny_Vraci1()
        => VyzvaCodeGenerator.DalsiPoradoveVRoce(Array.Empty<int>()).Should().Be(1);

    [Fact]
    public void DalsiPoradoveVRoce_NejvyssiPlus1()
        => VyzvaCodeGenerator.DalsiPoradoveVRoce(new[] { 1, 2, 5 }).Should().Be(6);
}
