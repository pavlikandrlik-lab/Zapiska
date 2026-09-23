using System.IO;
using FluentAssertions;
using PmTracker.Web.Models.Entities;
using PmTracker.Web.Services.Common;
using Xunit;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Common;

/// <summary>
/// Podbarvení ukončených úkolů (2026-09-05): číselník stavů úkolů se udržuje jen
/// v databázi, proto je rozhodování o pozastavení a ukončenosti na jednom místě.
/// </summary>
public sealed class TaskStatusRulesTests
{
    [Theory]
    [InlineData("Pozastaveno", true)]
    [InlineData("pozastaveno", true)]
    [InlineData("POZASTAVENO", true)]
    [InlineData("Dočasně pozastavený úkol", true)]
    [InlineData("Rozpracováno", false)]
    [InlineData("Ukončeno", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPausedName_DetectsPauseByName(string? nazev, bool expected)
        => TaskStatusRules.IsPausedName(nazev).Should().Be(expected);

    [Fact]
    public void IsCompleted_IsTrueForFinalState()
        => TaskStatusRules
            .IsCompleted(new CiselnikStavuUkoluEntity { Nazev = "Ukončeno", IsFinal = true })
            .Should().BeTrue();

    [Fact]
    public void IsCompleted_IsFalseForRunningState()
        => TaskStatusRules
            .IsCompleted(new CiselnikStavuUkoluEntity { Nazev = "Rozpracováno", IsFinal = false })
            .Should().BeFalse();

    [Fact]
    public void IsCompleted_IsFalseWhenPausedStateIsMarkedFinal()
        => TaskStatusRules
            .IsCompleted(new CiselnikStavuUkoluEntity { Nazev = "Pozastaveno", IsFinal = true })
            .Should().BeFalse(
                "rozhodnutí U1 — pozastavený stav nesmí platit za ukončený ani při rozporu v databázi");

    [Fact]
    public void IsCompleted_IsFalseForUnknownState()
        => TaskStatusRules.IsCompleted(null).Should().BeFalse();

    [Fact]
    public void Export_UsesSharedPauseRule_NotItsOwnLiteral()
    {
        var source = File.ReadAllText(ResolvePath("PmTracker.Web/Services/Export/ExportProjectionBuilders.cs"));

        source.Should().Contain("TaskStatusRules.IsPausedName",
            "export musí používat sdílené pravidlo");
        source.Should().NotContain("\"pozastav\"",
            "definice pozastavení smí být jen v TaskStatusRules, jinak se obě verze časem rozejdou");
    }
}
