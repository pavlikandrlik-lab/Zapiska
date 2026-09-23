using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace PmTracker.Tests.Unit.Schedule;

/// <summary>
/// Stránka záznamu (2026-07-14): tabulková sekce harmonogramu je vyčleněná do
/// samostatného partialu _ScheduleTable.cshtml, aby ji mohl použít i read-only
/// tabulkový režim na stránce záznamu. Extrakce je ČISTÝ PŘESUN — editor musí
/// tabulku dál renderovat beze změny.
/// </summary>
public sealed class ScheduleTableExtractionTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PmTracker.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root nenalezen");
    }

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string Table => Read("PmTracker.Web/Views/Shared/_ScheduleTable.cshtml");
    private static string Block => Read("PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml");

    [Fact]
    public void TablePartial_Exists_WithBlockViewModel()
    {
        Table.Should().Contain("@model HarmonogramBlockViewModel", "partial bere celý blok VM");
        Table.Should().Contain("schedule-table-wrap", "renderuje obal tabulky");
        Table.Should().Contain("<th>Krok</th>", "3sloupcová tabulka Krok / Plán / Skutečnost");
    }

    [Fact]
    public void Block_DelegatesTableToPartial_InEditorMode()
    {
        Block.Should().Contain("_ScheduleTable.cshtml", "editor mód deleguje tabulku na partial");
        Block.Should().NotContain("schedule-table-wrap",
            "tabulkový markup se přesunul do _ScheduleTable.cshtml (žádná duplikace)");
    }

    /// <summary>2026-09-03: zamčené datumové pole nabízelo křížek „vymazat", který nic nedělal.
    /// Clearable se váže na to, zda je pole odemčené.</summary>
    [Fact]
    public void LockedDateField_DoesNotOfferClearButton()
    {
        Regex.IsMatch(Table, @"Locked\s*=\s*delayDateDisabled,\s*Clearable\s*=\s*true")
            .Should().BeFalse("zamčené pole nesmí mít nefunkční křížek");
        Table.Should().Contain("Clearable = !delayDateDisabled",
            "křížek jen u odemčeného pole");
    }

    [Fact]
    public void TablePartial_KeepsEditorContract()
    {
        // Kontrakt form bindingu a stavové logiky se přesunem NESMÍ změnit.
        Table.Should().Contain("HarmonogramHodnoty[", "plánové datum drží form binding");
        Table.Should().Contain("var manualInputIndex = 0;", "čítač manual inputů se přesunul s tabulkou");
        Table.Should().Contain("Model.HideActual", "7b větev skrytí skutečnosti");
        Table.Should().Contain("EditorChangedTypeTooltips", "proposal-diff zvýraznění kroků");
        Table.Should().Contain("_ScheduleBlockManualCell", "manual/auto buňky renderuje původní sub-partial");
    }
}
