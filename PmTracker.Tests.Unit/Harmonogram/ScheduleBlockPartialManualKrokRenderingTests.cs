using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Harmonogram;

/// <summary>
/// Plán D Task 8: file-text snapshot _ScheduleBlock.cshtml vynucuje
/// tři stavy skutečnosti (FromVyjadreni / Manual / None) plus integrace
/// s pending návrh lockem a records.edit autorizací.
/// Snapshot styl (ne HTML rendering) — stejný pattern jako
/// <c>ChatModalStepperVazbaIdTests</c>, protože projekt nemá Razor
/// render test framework a infrastruktura by byla nepřiměřená.
/// </summary>
public sealed class ScheduleBlockPartialManualKrokRenderingTests
{
    private const string PartialPath = "PmTracker.Web/Views/Shared/_ScheduleBlock.cshtml";
    private const string ManualCellPartialPath = "PmTracker.Web/Views/Shared/_ScheduleBlockManualCell.cshtml";

    private static string LoadRepoText(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        var full = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(full).Should().BeTrue($"soubor musí existovat na cestě {full}");
        return File.ReadAllText(full);
    }

    /// <summary>Složený text obou partials — skutečnost se renderuje v jednom z nich.</summary>
    private static string LoadCombinedPartialText()
    {
        return LoadRepoText(PartialPath) + "\n" + LoadRepoText(ManualCellPartialPath);
    }

    [Fact]
    public void Partial_BranchesOn_ZdrojSkutecnosti()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain(
            "krok.ZdrojSkutecnosti",
            "Task 8: partial musí větvit render podle zdroje skutečnosti.");
    }

    [Fact]
    public void Partial_Renders_ChatIcon_For_FromVyjadreni_State()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain(
            "ZdrojSkutecnosti.FromVyjadreni",
            "Task 8: větev pro vazbu na vyjádření musí být čitelně pojmenovaná.");
        text.Should().Contain(
            "data-external-chat-open",
            "Task 8: klik na chat ikonu musí routovat přes existující handler.");
        text.Should().Contain(
            "data-external-odkaz-id",
            "Task 8: chat handler očekává data-external-odkaz-id atribut.");
    }

    [Fact]
    public void Partial_Renders_WarningIcon_For_None_State()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain(
            "ZdrojSkutecnosti.None",
            "Task 8: musí existovat větev pro None (auto krok bez vazby).");
        text.Should().Contain(
            "data-manual-krok-warning",
            "Task 8: varovná ikona musí mít stabilní hook pro JS/testy.");
    }

    [Fact]
    public void Partial_Renders_DateInput_For_Manual_State()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain(
            "data-manual-krok-input",
            "Task 8: JS modul manualKroky.js se váže přes data-manual-krok-input.");
        text.Should().Contain(
            "ManualActualKroky[",
            "Datum-model: form POST váže pole na ManualActualKroky[i].Poradi/AbsolutniDatum.");
        text.Should().Contain(
            ".Poradi",
            "Datum-model: pořadí kroku (1–10) se posílá jako hidden místo Guid KrokKey.");
        text.Should().Contain(
            ".AbsolutniDatum",
            "Task 8: AbsolutniDatum se posílá jako yyyy-MM-dd z date inputu.");
    }

    [Fact]
    public void Partial_Respects_Pending_Proposal_Lock()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain(
            "LockedManualKrokKeys",
            "Task 8: partial musí respektovat lock z PendingScheduleProposalLockState.");
    }

    [Fact]
    public void Partial_Respects_Records_Edit_Permission()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain(
            "CanEditManualActual",
            "Task 8: partial musí číst bool z VM; permission check patří do composition.");
    }

    [Fact]
    public void Partial_ReadOnly_ManualKrok_RenderedAsFormattedSpan_Not_Input()
    {
        var text = LoadCombinedPartialText();
        text.Should().Contain(
            "data-manual-krok-readonly",
            "Task 8: read-only stav (bez records.edit nebo při locku) má vlastní hook, ne input.");
    }
}
