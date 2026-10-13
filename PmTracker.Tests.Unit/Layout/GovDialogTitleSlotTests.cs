using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Layout;

/// <summary>
/// gov-dialog (DS gov 4.7.0) vykresluje nadpis jen ze slotu "title" — slot "label" nemá, takže
/// takový nadpis zůstane neviditelný (dialog „Návrh nelze založit“ a0c0cb5; okno vyjádření
/// a dialog neuložených změn 2026-10-13). Hlídá JS moduly, které gov-dialog vytvářejí.
/// </summary>
public sealed class GovDialogTitleSlotTests
{
    private static readonly Regex LabelSlot = new(
        @"slot=""label""|setAttribute\(\s*[""']slot[""']\s*,\s*[""']label[""']\s*\)",
        RegexOptions.Compiled);

    [Fact]
    public void JsModulySGovDialogem_NadpisVeSlotuTitle()
    {
        var modules = Directory.GetFiles(ResolvePath("PmTracker.Web/wwwroot/js/modules"), "*.js", SearchOption.AllDirectories);
        var offenders = modules
            .Where(path =>
            {
                var js = File.ReadAllText(path);
                return js.Contains("gov-dialog") && LabelSlot.IsMatch(js);
            })
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToList();

        // Join: hláška vyjmenuje všechny soubory (BeEmpty ukáže jen první).
        string.Join(", ", offenders).Should().BeEmpty("nadpis gov-dialogu patří do slotu \"title\"");
    }
}
