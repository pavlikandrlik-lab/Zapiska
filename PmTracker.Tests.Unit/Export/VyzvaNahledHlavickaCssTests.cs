using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using static PmTracker.Tests.Unit.Architecture.ArchitectureTestBase;

namespace PmTracker.Tests.Unit.Export;

/// <summary>
/// Náhled výzvy má hlavičku úřadu stejnou jako Word a vzor (2026-10-06): na střed, první řádek
/// proložený o 2 body, pod adresou čára.
/// </summary>
public sealed class VyzvaNahledHlavickaCssTests
{
    private static string Pravidlo(string selektor)
    {
        var css = File.ReadAllText(ResolvePath("PmTracker.Web/wwwroot/css/pdf-export.css"));
        var shoda = Regex.Match(css, Regex.Escape(selektor) + @"\s*\{(?<telo>[^}]*)\}");
        shoda.Success.Should().BeTrue($"pravidlo {selektor} musí v pdf-export.css existovat");
        return shoda.Groups["telo"].Value;
    }

    [Fact]
    public void Hlavicka_NaStred()
        => Pravidlo(".vyzva-hlavicka").Should().Contain("text-align: center", "vzor má hlavičku úřadu na střed");

    [Fact]
    public void PrvniRadek_ProlozenyODvaBody()
        => Pravidlo(".vyzva-hlavicka-urad").Should().Contain("letter-spacing: 2pt");

    [Fact]
    public void PodAdresou_Cara()
    {
        Pravidlo(".vyzva-hlavicka-adresa").Should().Contain("border-bottom: 1.5pt solid");
        File.ReadAllText(ResolvePath("PmTracker.Web/Views/Export/VyzvaTemplate.cshtml"))
            .Should().Contain("class=\"vyzva-male vyzva-hlavicka-adresa\"");
    }
}
