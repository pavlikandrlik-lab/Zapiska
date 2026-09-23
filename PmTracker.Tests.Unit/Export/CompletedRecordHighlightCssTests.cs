using System.IO;
using FluentAssertions;
using Xunit;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Podbarvení ukončených (2026-09-05): jemné modré pozadí řádku a pořadí pravidel,
/// aby při souběhu vyhrálo pozastavení (spec §4.3).
/// </summary>
public sealed class CompletedRecordHighlightCssTests
{
    private static string Css() => File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/pdf-export.css"));

    [Fact]
    public void Css_ShadesCompletedRowWithPaleBlue()
    {
        var css = Css();

        css.Should().Contain(".task-row.completed td");
        css.Should().Contain("#eff6ff", "jemná modrá odpovídá váze stávající krémové u pozastavených");
    }

    [Fact]
    public void Css_LetsPausedWinOverCompleted()
    {
        var css = Css();
        var completedIndex = css.IndexOf(".task-row.completed td", StringComparison.Ordinal);
        var pausedIndex = css.IndexOf(".task-row.paused td", StringComparison.Ordinal);

        completedIndex.Should().BeGreaterThan(-1);
        pausedIndex.Should().BeGreaterThan(-1);
        completedIndex.Should().BeLessThan(pausedIndex,
            "obě pravidla mají stejnou specificitu, takže rozhoduje pořadí — pozastavení musí vyhrát");
    }

    [Fact]
    public void Template_MarksCompletedRow()
    {
        var view = File.ReadAllText(ResolvePath("PmTracker.Web/Views/Export/_PdfRecordRow.cshtml"));

        view.Should().Contain("Model.IsCompleted");
        view.Should().Contain("completed");
    }

    [Fact]
    public void StartupValidator_WarnsAboutContradictoryTaskStates()
    {
        var source = File.ReadAllText(
            ResolvePath("PmTracker.Web/Services/Data/SqlStartupValidatorHostedService.cs"));

        source.Should().Contain("TaskStatusRules.IsPausedName",
            "varování musí používat stejné pravidlo jako export, jinak se definice rozejdou");
        source.Should().Contain("CiselnikStavuUkolu",
            "kontroluje se číselník stavů úkolů");
        source.Should().Contain("LogWarning",
            "rozporný stav aplikaci nezastaví, jen se zaloguje");
    }
}
