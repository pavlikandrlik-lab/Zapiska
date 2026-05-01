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

        // Phase 9 (DESIGN-9-D, 2026-05-01): žádné skrývání kroků z žádného důvodu.
        // visibleCompactSteps používá kompletní Model.Kroky bez filteru
        // (memory project_harmonogram_visibility_rules: žádné skrývání kroků
        // podle typu napojeného ticketu nebo nulového trvání).
        source.Should().NotContain(
            "Model.Kroky.Where(krok => krok.TrvaniDni > 0)",
            "compact rainbow strip nesmí filtrovat zero-duration (DESIGN-9-D)");
        source.Should().Contain(
            "var visibleCompactSteps = Model.Kroky",
            "compact rainbow musí použít všechny kroky bez Where filtru");
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
    public void BreakdownSteps_ShouldRenderAllSteps_NoFilter()
    {
        var source = LoadScheduleBlockSource();

        // Phase 9 (DESIGN-9-D, 2026-05-01): breakdown nesmí filtrovat. Render všech
        // kroků včetně zero-duration / NULL OdchylkaDni — řízení viditelnosti přes
        // CSS (width:0 / visibility:hidden pro zero plan, žádný actual segment pro NULL).
        source.Should().NotContain(
            "Model.Kroky.Where(k => k.TrvaniDni > 0)",
            "breakdown nesmí filtrovat zero-duration řádky (DESIGN-9-D — žádné skrývání)");
        source.Should().Contain(
            "data-step-has-plan",
            "breakdown vystavuje data-step-has-plan attribute pro JS layout");
        source.Should().Contain(
            "data-step-has-actual",
            "breakdown vystavuje data-step-has-actual attribute (NULL OdchylkaDni → false)");
    }
}
