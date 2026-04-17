using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Schedule;

public sealed class ScheduleBlockMarkupTests
{
    private static string LoadScheduleBlockSource()
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

        var viewPath = Path.Combine(
            directory.FullName,
            "PmTracker.Web",
            "Views",
            "Shared",
            "_ScheduleBlock.cshtml");

        File.Exists(viewPath).Should().BeTrue($"_ScheduleBlock.cshtml musí existovat na cestě {viewPath}");
        return File.ReadAllText(viewPath);
    }

    [Fact]
    public void EditorMode_ShouldRenderAllSteps_IncludingZeroDuration()
    {
        var source = LoadScheduleBlockSource();

        // V editoru se musí renderovat segmenty pro VŠECHNY kroky (i zero-duration),
        // aby je JS mohl zobrazit, když uživatel zadá kladné trvání.
        source.Should().Contain(
            "isEditor",
            "musí existovat větvení podle editor režimu");
        source.Should().Contain(
            "Model.Kroky.Where(krok => krok.TrvaniDni > 0)",
            "readonly varianta musí filtrovat zero-duration kroky");
        source.Should().MatchRegex(
            "isEditor\\s*\\?\\s*Model\\.Kroky\\s*:\\s*Model\\.Kroky\\.Where",
            "v editoru se musí použít kompletní Model.Kroky bez filtru zero-duration");
    }

    [Fact]
    public void ZeroDurationSegments_ShouldBeHiddenViaInlineStyle()
    {
        var source = LoadScheduleBlockSource();

        // Server-rendered zero-duration segmenty v editoru mají display:none jako inline style.
        // JS je pak zobrazí přes applySegmentLayout, jakmile uživatel zadá kladné trvání.
        source.Should().Contain(
            "initialHidden",
            "musí existovat proměnná initialHidden pro zero-duration segmenty");
        source.Should().Contain(
            "display:none;",
            "zero-duration segmenty musí být skryty přes display:none");
    }

    [Fact]
    public void BreakdownSteps_ShouldFilterZeroDurationRows()
    {
        var source = LoadScheduleBlockSource();

        // Breakdown panel (readonly, rozbalený detail karty) nesmí renderovat řádky
        // pro zero-duration kroky — jinak vznikají prázdné mezery.
        source.Should().MatchRegex(
            "Model\\.Kroky\\.Where\\(k => k\\.TrvaniDni > 0\\)\\.OrderBy\\(k => k\\.KrokIndex\\)",
            "gantt-steps breakdown musí filtrovat kroky s kladným trváním");
    }
}
